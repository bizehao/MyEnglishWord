using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MyEnglishWord.Core.DataBase;
using MyEnglishWord.Core.Models;
using MyEnglishWord.Models;
using Windows.Media.Ocr;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MyEnglishWord.Dialog;

public sealed partial class WordOpDialog : ContentDialog
{
    public enum Type
    {
        Update,
        Insert
    }

    public Type OpType
    {
        get;
    }

    public WordModel CurWord
    {
        get;
    }

    public string WordTitle
    {
        get;
    }
    

    public bool IsUpdate => OpType == Type.Update;

    public static WordOpDialog MakeInsertWordDialog()
    {
        return new WordOpDialog(Type.Insert, new WordModel());
    }

    public static WordOpDialog MakeUpdateWordDialog(WordModel source)
    {
        var word = new WordModel()
        {
            Id = source.Id,
            Word = source.Word,
            Phonetic = source.Phonetic,
            Definition = source.Definition,
            Translation = source.Translation,
            Pos = source.Pos,
            Collins = source.Collins,
            Oxford = source.Oxford,
            Tag = source.Tag,
            Bnc = source.Bnc,
            Frq = source.Frq,
            Exchange = source.Exchange,
            Detail = source.Detail
        };
        return new WordOpDialog(Type.Update, word);
    }

    private WordOpDialog(Type type, WordModel word)
    {
        InitializeComponent();
        OpType = type;
        CurWord = word;
        WordTitle = type switch
        {
            Type.Insert => $"增添单词",
            Type.Update => $"更新单词: {CurWord.Word}",
            _ => throw new NotImplementedException()
        };
    }

   private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var ctx = App.GetService<EnglishWordContext>();
        var entry = new WordEntry()
        {
            Id = CurWord.Id,
            Word = CurWord.Word,
            Sw = CurWord.Word,
            Phonetic = CurWord.Phonetic,
            Definition = CurWord.Definition,
            Translation = CurWord.Translation,
            Pos = CurWord.Pos,
            Collins = CurWord.Collins,
            Oxford = CurWord.Oxford ? 1 : 0,
            Tag = CurWord.Tag,
            Bnc = CurWord.Bnc,
            Frq = CurWord.Frq,
            Exchange = CurWord.Exchange,
            Detail = CurWord.Detail
        };

        if (OpType == Type.Update)
        {
            await ctx.Update(entry);

        }
        else if (OpType == Type.Insert)
        {
            await ctx.Insert(entry);
        }
        else {
            throw new NotImplementedException();
        }
    }

    private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
    }


}
