using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace MyEnglishWord.Helpers;

public static class AncestorSource
{
    public static readonly DependencyProperty AncestorTypeProperty =
        DependencyProperty.RegisterAttached(
            "AncestorType",
            typeof(Type),
            typeof(AncestorSource),
            new PropertyMetadata(default(Type), OnAncestorTypeChanged)
    );

    public static void SetAncestorType(FrameworkElement element, Type value) =>
        element.SetValue(AncestorTypeProperty, value);

    public static Type GetAncestorType(FrameworkElement element) =>
        (Type)element.GetValue(AncestorTypeProperty);

    private static void OnAncestorTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        FrameworkElement target = (FrameworkElement)d;
        if (target.IsLoaded)
            SetDataContext(target);
        else
            target.Loaded += OnTargetLoaded;
    }

    private static void OnTargetLoaded(object sender, RoutedEventArgs e)
    {
        FrameworkElement target = (FrameworkElement)sender;
        target.Loaded -= OnTargetLoaded;
        SetDataContext(target);
    }

    private static void SetDataContext(FrameworkElement target)
    {
        Type ancestorType = GetAncestorType(target);
        if (ancestorType != null)
        {
            var targetEle = FindParent(target, ancestorType);
            if (targetEle is not null && targetEle is FrameworkElement ele)
            {
                target.DataContext = ele.DataContext;
            }
        } 
    }

    private static object FindParent(DependencyObject dependencyObject, Type ancestorType)
    {
        DependencyObject parent = VisualTreeHelper.GetParent(dependencyObject);
        if (parent == null)
            return null;

        if (ancestorType.IsAssignableFrom(parent.GetType()))
            return parent;

        return FindParent(parent, ancestorType);
    }
}
