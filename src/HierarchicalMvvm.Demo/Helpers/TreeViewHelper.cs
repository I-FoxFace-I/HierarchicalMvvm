using System.Windows;
using System.Windows.Controls;

namespace HierarchicalMvvm.Demo.Helpers;
public static class TreeViewHelper
{
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.RegisterAttached(
            "SelectedItem",
            typeof(object),
            typeof(TreeViewHelper),
            new UIPropertyMetadata(null, OnSelectedItemChanged));

    public static object GetSelectedItem(DependencyObject obj) =>
        obj.GetValue(SelectedItemProperty);

    public static void SetSelectedItem(DependencyObject obj, object value) =>
        obj.SetValue(SelectedItemProperty, value);

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var treeView = d as TreeView;
        if (treeView != null)
        {
            treeView.SelectedItemChanged += (s, args) =>
            {
                SetSelectedItem(treeView, args.NewValue);
            };
        }
    }
}
