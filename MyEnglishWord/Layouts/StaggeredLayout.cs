// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace MyEnglishWord.Layout;

/// <summary>
/// Arranges child elements into a staggered grid pattern where items are added to the column that has used least amount of space.
/// </summary>
public partial class StaggeredLayout : NonVirtualizingLayout
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StaggeredLayout"/> class.
    /// </summary>
    public StaggeredLayout()
    {
    }

    /// <summary>
    /// Gets or sets the desired width for each column.
    /// </summary>
    public StaggeredLayoutItemsStretch ItemsStretch
    {
        get => (StaggeredLayoutItemsStretch)GetValue(ItemsStretchProperty);
        set => SetValue(ItemsStretchProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="ItemsStretch"/> dependency property.
    /// </summary>
    /// <returns>The identifier for the <see cref="DesiredColumnWidth"/> dependency property.</returns>
    public static readonly DependencyProperty ItemsStretchProperty = DependencyProperty.Register(
        nameof(ItemsStretch),
        typeof(StaggeredLayoutItemsStretch),
        typeof(StaggeredLayout),
        new PropertyMetadata(StaggeredLayoutItemsStretch.None, OnItemsStretchChanged));

    /// <summary>
    /// Gets or sets the desired width for each column.
    /// </summary>
    public double DesiredColumnWidth
    {
        get => (double)GetValue(DesiredColumnWidthProperty);
        set => SetValue(DesiredColumnWidthProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="DesiredColumnWidth"/> dependency property.
    /// </summary>
    /// <returns>The identifier for the <see cref="DesiredColumnWidth"/> dependency property.</returns>
    public static readonly DependencyProperty DesiredColumnWidthProperty = DependencyProperty.Register(
        nameof(DesiredColumnWidth),
        typeof(double),
        typeof(StaggeredLayout),
        new PropertyMetadata(250d, OnDesiredColumnWidthChanged));

    /// <summary>
    /// Gets or sets the spacing between columns of items.
    /// </summary>
    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="ColumnSpacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ColumnSpacingProperty = DependencyProperty.Register(
        nameof(ColumnSpacing),
        typeof(double),
        typeof(StaggeredLayout),
        new PropertyMetadata(0d, OnSpacingChanged));

    /// <summary>
    /// Gets or sets the spacing between rows of items.
    /// </summary>
    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="RowSpacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty RowSpacingProperty = DependencyProperty.Register(
        nameof(RowSpacing),
        typeof(double),
        typeof(StaggeredLayout),
        new PropertyMetadata(0d, OnSpacingChanged));

    /// <inheritdoc/>
    protected override void InitializeForContextCore(NonVirtualizingLayoutContext context)
    {
        context.LayoutState = new StaggeredLayoutState();
        base.InitializeForContextCore(context);
    }

    /// <inheritdoc/>
    protected override void UninitializeForContextCore(NonVirtualizingLayoutContext context)
    {
        context.LayoutState = null;
        base.UninitializeForContextCore(context);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
    {
        var state = (StaggeredLayoutState)context.LayoutState!;
        state.Clear();

        if (context.Children.Count == 0)
        {
            return new Size(availableSize.Width, 0);
        }

        double availableWidth = availableSize.Width;
        double availableHeight = availableSize.Height;

        double columnWidth;
        int numColumns;
        if (ItemsStretch is StaggeredLayoutItemsStretch.None)
        {
            columnWidth = double.IsNaN(DesiredColumnWidth) ? availableWidth : Math.Min(DesiredColumnWidth, availableWidth);
            numColumns = Math.Max(1, (int)Math.Floor(availableWidth / (columnWidth + ColumnSpacing)));
        }
        else
        {
            if (double.IsNaN(DesiredColumnWidth) || DesiredColumnWidth > availableWidth)
            {
                columnWidth = availableWidth;
                numColumns = 1;
            }
            else
            {
                // 0.0001 is to prevent floating point errors
                var tempAvailableWidth = availableWidth + ColumnSpacing - 0.0001;
                numColumns = (int)Math.Floor(tempAvailableWidth / (DesiredColumnWidth + ColumnSpacing));
                columnWidth = tempAvailableWidth / numColumns - ColumnSpacing;
            }
        }
        
        state.ColumnWidth = columnWidth;

        // adjust for column spacing on all columns expect the first
        double totalWidth = state.ColumnWidth + ((numColumns - 1) * (state.ColumnWidth + ColumnSpacing));
        if (totalWidth > availableWidth)
        {
            numColumns--;
        }
        else if (double.IsInfinity(availableWidth))
        {
            availableWidth = totalWidth;
        }

        var columnHeights = new double[numColumns];
        var itemsPerColumn = new int[numColumns];

        for (int i = 0; i < context.Children.Count; i++)
        {
            var columnIndex = GetColumnIndex(columnHeights);
            StaggeredItem item = state.GetItemAt(i);
            UIElement element = context.Children[i];
            element.Measure(new Size(state.ColumnWidth, availableHeight));
            item.Height = element.DesiredSize.Height;

            double spacing = itemsPerColumn[columnIndex] > 0 ? RowSpacing : 0;
            item.Top = columnHeights[columnIndex] + spacing;
            columnHeights[columnIndex] = item.Top + item.Height;
            itemsPerColumn[columnIndex]++;
            state.AddItemToColumn(item, columnIndex);
        }

        double desiredHeight = state.GetHeight();

        return new Size(availableWidth, desiredHeight);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize)
    {
        var state = (StaggeredLayoutState)context.LayoutState!;

        for (int columnIndex = 0; columnIndex < state.NumberOfColumns; columnIndex++)
        {
            StaggeredColumnLayout layout = state.GetColumnLayout(columnIndex);
            foreach (StaggeredItem item in layout)
            {
                double itemHorizontalOffset = (state.ColumnWidth * columnIndex) + (ColumnSpacing * columnIndex);
                Rect bounds = new Rect((float)itemHorizontalOffset, (float)item.Top, (float)state.ColumnWidth, (float)item.Height);
                context.Children[item.Index].Arrange(bounds);
            }
        }

        return finalSize;
    }

    private static void OnItemsStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (StaggeredLayout)d;
        panel.InvalidateMeasure();
    }

    private static void OnDesiredColumnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (StaggeredLayout)d;
        panel.InvalidateMeasure();
    }

    private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (StaggeredLayout)d;
        panel.InvalidateMeasure();
    }

    private static int GetColumnIndex(double[] columnHeights)
    {
        int columnIndex = 0;
        double height = columnHeights[0];
        for (int j = 1; j < columnHeights.Length; j++)
        {
            if (columnHeights[j] < height)
            {
                columnIndex = j;
                height = columnHeights[j];
            }
        }

        return columnIndex;
    }
}
