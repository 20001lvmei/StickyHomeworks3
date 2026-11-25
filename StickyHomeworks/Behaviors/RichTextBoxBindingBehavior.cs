using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Xaml.Behaviors;

namespace StickyHomeworks.Behaviors;

public class RichTextBoxBindingBehavior : Behavior<RichTextBox>
{
    private static readonly HashSet<Thread> _recursionProtection = new HashSet<Thread>();

    protected override void OnAttached()
    {
        AssociatedObject.TextChanged += OnTextChanged;

        // 绑定值可能在挂载之前就已经设置过，这里补一次同步
        ApplyDocument();
        base.OnAttached();
    }

    protected override void OnDetaching()
    {
        AssociatedObject.TextChanged -= OnTextChanged;
        base.OnDetaching();
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not RichTextBox richTextBox)
        {
            return;
        }

        SetDocumentXaml(this, XamlWriter.Save(richTextBox.Document));
    }

    /// <summary>
    /// 把 DocumentXaml 解析成文档赋给富文本框。控件未挂载时直接跳过。
    /// </summary>
    private void ApplyDocument()
    {
        var richTextBox = AssociatedObject;
        if (richTextBox == null)
        {
            return;
        }

        try
        {
            richTextBox.Document = RichTextBoxHelper.ConvertDocument(GetDocumentXaml(this));
            richTextBox.Document.IsOptimalParagraphEnabled = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RichTextBoxBindingBehavior] 更新文档失败：{ex.Message}");
        }
    }

    public static string GetDocumentXaml(DependencyObject obj)
    {
        return (string?)obj.GetValue(DocumentXamlProperty) ?? "";
    }

    public static void SetDocumentXaml(DependencyObject obj, string value)
    {
        _recursionProtection.Add(Thread.CurrentThread);
        obj.SetValue(DocumentXamlProperty, value);
        _recursionProtection.Remove(Thread.CurrentThread);
    }

    public static readonly DependencyProperty DocumentXamlProperty = DependencyProperty.Register(
        nameof(DocumentXaml), typeof(string), typeof(RichTextBoxBindingBehavior), new FrameworkPropertyMetadata(
            "",
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (obj, e) =>
            {
                if (_recursionProtection.Contains(Thread.CurrentThread))
                {
                    return;
                }

                if (obj is not RichTextBoxBindingBehavior behavior)
                {
                    return;
                }

                // 控件还没挂上（列表项回收/尚未加载）时忽略，OnAttached 里会再同步一次
                if (behavior.AssociatedObject == null)
                {
                    return;
                }

                behavior.ApplyDocument();
            }
        ));

    public string DocumentXaml
    {
        get => GetDocumentXaml(this);
        set => SetValue(DocumentXamlProperty, value);
    }
}
