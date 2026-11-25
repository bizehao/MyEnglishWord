using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using MyEnglishWord.Helpers;
using MyEnglishWord.Models;
using static MyEnglishWord.Models.SynsetModel;

namespace MyEnglishWord.Styles;

public sealed partial class MainPageStyle : ResourceDictionary
{
    public MainPageStyle()
    {
        this.InitializeComponent();
    }

    private async void ExampleRichTextBlock_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        // TODO
        // Remove it
        CancellationTokenSource tempSource = new CancellationTokenSource();

        if (sender is not RichTextBlock richTextBlock)
        {
            return;
        }
        richTextBlock.Blocks.Clear();

        var aa = sender.DataContext;


        if (args.NewValue != null && args.NewValue is ExampleModel exampleModel)
        {
            var exampleStr = exampleModel.Example;

            var highlightRanges = await Misc.HightlightHelperAsync(exampleStr, exampleModel.Words.ToArray(), tempSource.Token);

            Paragraph paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run
            {
                Text = "\""
            });
            for (int highlightRangeIndex = 0; highlightRangeIndex < highlightRanges.Count; ++highlightRangeIndex)
            {
                int preHighlightStart = 0;
                if (highlightRangeIndex > 0)
                {
                    preHighlightStart = highlightRanges[highlightRangeIndex - 1].startIndex + highlightRanges[highlightRangeIndex - 1].length;
                }
                int preHighlightLength = highlightRanges[highlightRangeIndex].startIndex - preHighlightStart;

                if (preHighlightLength > 0)
                {
                    Run preHighlightRun = new Run();
                    preHighlightRun.Text = exampleStr.Substring(preHighlightStart, preHighlightLength);
                    paragraph.Inlines.Add(preHighlightRun);
                }

                Run highlightRun = new Run();
                highlightRun.FontWeight = FontWeights.Bold;
                highlightRun.Text = exampleStr.Substring(highlightRanges[highlightRangeIndex].startIndex, highlightRanges[highlightRangeIndex].length);
                paragraph.Inlines.Add(highlightRun);
            }

            int postHighlightStart = 0;
            int postHighlightLength = exampleStr.Length;

            if (highlightRanges.Count > 0)
            {
                postHighlightStart = highlightRanges[highlightRanges.Count - 1].startIndex + highlightRanges[highlightRanges.Count - 1].length;
                postHighlightLength = exampleStr.Length - postHighlightStart;
            }

            if (postHighlightLength > 0)
            {
                Run postHighlightRun = new Run();
                postHighlightRun.Text = exampleStr.Substring(postHighlightStart, postHighlightLength);
                paragraph.Inlines.Add(postHighlightRun);
            }
            paragraph.Inlines.Add(new Run
            {
                Text = "\""
            });
            richTextBlock.Blocks.Add(paragraph);
        }
    }
}
