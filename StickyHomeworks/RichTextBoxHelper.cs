using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Xml;

namespace StickyHomeworks;

public static class RichTextBoxHelper
{
    private const string PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string EmojiNamespacePrefix = "clr-namespace:Emoji.Wpf";

    /// <summary>
    /// 允许出现在作业内容中的文档模型元素。
    /// </summary>
    private static readonly HashSet<string> AllowedDocumentElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "FlowDocument", "Section", "Paragraph", "Span", "Run", "Bold", "Italic", "Underline",
        "LineBreak", "Hyperlink", "List", "ListItem", "Table", "TableRowGroup", "TableRow",
        "TableCell", "TableColumn", "TableColumnGroup", "Figure", "Floater",
        "InlineUIContainer", "BlockUIContainer",
        "SolidColorBrush", "LinearGradientBrush", "RadialGradientBrush", "GradientStop",
        "TextDecorationCollection", "TextDecoration", "FontFamily"
    };

    /// <summary>
    /// 允许出现在作业内容中的表情元素（Emoji.Wpf 会把表情渲染成内联控件）。
    /// </summary>
    private static readonly HashSet<string> AllowedEmojiElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "Image", "EmojiInline"
    };

    /// <summary>
    /// 允许的属性值关键字。出现这些内容说明 XAML 在被用作“代码”而不是“文档”。
    /// </summary>
    private static readonly string[] ForbiddenMarkers =
    {
        "ObjectDataProvider", "XamlReader", "x:Class", "x:Code", "Process", "Assembly.Load"
    };

    private const int MaxDepth = 64;

    /// <summary>
    /// 把作业内容（FlowDocument 的 XAML 字符串）转换为 <see cref="FlowDocument"/>。
    /// <para>
    /// 内容来自本地 Profile.json，理论上可能被替换。这里禁止 DTD/外部实体，并只放行文档模型
    /// 与 Emoji.Wpf 的元素，避免通过 XAML 实例化任意类型；不合法的内容会退化为纯文本，不会丢数据。
    /// </para>
    /// </summary>
    public static FlowDocument ConvertDocument(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml))
        {
            return CreateEmptyDocument();
        }

        if (!IsSafeDocumentXaml(xaml))
        {
            Debug.WriteLine("[RichTextBoxHelper] 作业内容包含不被允许的 XAML，已按纯文本显示。");
            return CreatePlainTextDocument(xaml);
        }

        try
        {
            using var stringReader = new StringReader(xaml);
            using var xmlReader = XmlReader.Create(stringReader, CreateXmlReaderSettings());
            var document = (FlowDocument)XamlReader.Load(xmlReader);
            document.IsOptimalParagraphEnabled = true;
            return document;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RichTextBoxHelper] 解析作业内容失败：{ex.Message}");
            return CreatePlainTextDocument(xaml);
        }
    }

    private static XmlReaderSettings CreateXmlReaderSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersFromEntities = 0,
        // 空白与注释是文档内容的一部分，不能被丢弃
        IgnoreComments = false,
        IgnoreWhitespace = false,
        CloseInput = false
    };

    /// <summary>
    /// 校验 XAML 只使用了白名单内的元素与命名空间。
    /// </summary>
    private static bool IsSafeDocumentXaml(string xaml)
    {
        try
        {
            using var stringReader = new StringReader(xaml);
            using var reader = XmlReader.Create(stringReader, CreateXmlReaderSettings());

            var depth = 0;
            var rootChecked = false;

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        depth--;
                    }
                    continue;
                }

                depth++;
                if (depth > MaxDepth)
                {
                    return false;
                }

                if (!IsAllowedElement(reader.NamespaceURI, reader.LocalName))
                {
                    return false;
                }

                if (!rootChecked)
                {
                    // 根节点必须是 FlowDocument，保证结果可以安全地转换成文档
                    if (!string.Equals(reader.LocalName, "FlowDocument", StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(reader.NamespaceURI, PresentationNamespace, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    rootChecked = true;
                }

                if (reader.HasAttributes && !AreAttributesAllowed(reader))
                {
                    return false;
                }
            }

            return rootChecked && depth == 0;
        }
        catch (XmlException ex)
        {
            Debug.WriteLine($"[RichTextBoxHelper] 作业内容不是合法的 XML：{ex.Message}");
            return false;
        }
    }

    private static bool IsAllowedElement(string namespaceUri, string localName)
    {
        if (string.Equals(namespaceUri, PresentationNamespace, StringComparison.OrdinalIgnoreCase))
        {
            return AllowedDocumentElements.Contains(localName);
        }

        if (namespaceUri.StartsWith(EmojiNamespacePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AllowedEmojiElements.Contains(localName);
        }

        if (string.Equals(namespaceUri, XamlNamespace, StringComparison.OrdinalIgnoreCase))
        {
            // x: 命名空间下不允许多余元素
            return false;
        }

        return false;
    }

    private static bool AreAttributesAllowed(XmlReader reader)
    {
        if (!reader.MoveToFirstAttribute())
        {
            return true;
        }

        try
        {
            do
            {
                // 命名空间声明（xmlns、xmlns:x）总是允许
                if (reader.Prefix == "xmlns" ||
                    string.Equals(reader.NamespaceURI, "http://www.w3.org/2000/xmlns/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // x:Class 等会让 XAML 具有“代码”语义，直接拒绝
                if (string.Equals(reader.Prefix, "x", StringComparison.OrdinalIgnoreCase) &&
                    reader.LocalName is "Class" or "Code" or "FieldModifier")
                {
                    return false;
                }

                foreach (var marker in ForbiddenMarkers)
                {
                    if (reader.Value.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            } while (reader.MoveToNextAttribute());
        }
        finally
        {
            reader.MoveToElement();
        }

        return true;
    }

    private static FlowDocument CreateEmptyDocument()
    {
        var document = new FlowDocument { IsOptimalParagraphEnabled = true };
        document.Blocks.Add(new Paragraph());
        return document;
    }

    private static FlowDocument CreatePlainTextDocument(string text)
    {
        var document = new FlowDocument { IsOptimalParagraphEnabled = true };
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(text);
        document.Blocks.Add(paragraph);
        return document;
    }
}
