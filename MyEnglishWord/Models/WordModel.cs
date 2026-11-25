using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using MyEnglishWord.Core.Models;
using MyEnglishWord.Models;
using static MyEnglishWord.ViewModels.MainViewModel;

namespace MyEnglishWord.Models;

public sealed partial class WordModel : ObservableObject
{
    //单词数据库id
    [ObservableProperty]
    public partial long Id
    {
        get;
        set;
    }

    //单词名称
    [ObservableProperty]
    public partial string Word
    {
        get;
        set;
    }

    //	音标，以英语英标为主
    [ObservableProperty]
    public partial string? Phonetic
    {
        get;
        set;
    }

    //单词释义（英文），每行一个释义
    [ObservableProperty]
    public partial string? Definition
    {
        get;
        set;
    }

    //单词释义（中文），每行一个释义
    [ObservableProperty]
    public partial string? Translation
    {
        get;
        set;
    }

    //词语位置，用 "/" 分割不同位置
    [ObservableProperty]
    public partial string? Pos
    {
        get;
        set;
    }

    //柯林斯星级
    [ObservableProperty]
    public partial int Collins
    {
        get;
        set;
    }

    //是否是牛津三千核心词汇
    [ObservableProperty]
    public partial bool Oxford
    {
        get;
        set;
    }

    //字符串标签：zk/中考，gk/高考，cet4/四级 等等标签，空格分割
    [ObservableProperty]
    public partial string? Tag
    {
        get;
        set;
    }

    //英国国家语料库词频顺序
    [ObservableProperty]
    public partial int Bnc
    {
        get;
        set;
    }

    //当代语料库词频顺序
    [ObservableProperty]
    public partial int Frq
    {
        get;
        set;
    }

    //时态复数等变换，使用 "/" 分割不同项目，见后面表格
    [ObservableProperty]
    public partial string? Exchange
    {
        get;
        set;
    }

    //json 扩展信息，字典形式保存例句（待添加）
    [ObservableProperty]
    public partial string? Detail
    {
        get;
        set;
    }
    //扩展
    public ObservableCollection<string> Morphs
    {
        get;
    } = [];
    //扩展
    public ObservableCollection<SynsetModel> Synsets
    {
        get;
    } = [];

    [ObservableProperty]
    public partial bool InDictionary
    {
        get;
        set;
    } = false;

    public bool LoadExtent
    {
        get;
        set;
    } = false;

    public static WordModel Entry { get; } = new();

    public static WordModel MakeWordModel(WordEntry entry)
    {
        return new()
        {
            Id = entry.Id,
            Word = entry.Word,
            Phonetic = entry.Phonetic,
            Definition = entry.Definition,
            Translation = entry.Translation,
            Pos = entry.Pos,
            Collins = entry.Collins,
            Oxford = entry.Oxford is > 0,
            Tag = entry.Tag,
            Bnc = entry.Bnc,
            Frq = entry.Frq,
            Exchange = entry.Exchange,
            Detail = entry.Detail is { Length: > 0 } ? entry.Detail : "无"
        };
    }


}


//public partial class ExampleViewModel : ObservableObject
//{
//    [ObservableProperty]
//    public partial string Example
//    {
//        get;
//        set;
//    } = string.Empty;

//    [ObservableProperty]
//    public partial string[] Words
//    {
//        get;
//        set;
//    } = [];

//}

//public partial class SynsetViewModel : ObservableObject
//{
//    public SynsetViewModel(SynsetModel synsetModel, string queryText, WordNetSqlite.PartOfSpeech pos, uint index)
//    {
//        SynsetModel = synsetModel;
//        QueryText = queryText;
//        Pos = pos;
//        IndexInQuery = index;

//        if (SynsetModel.Examples != null)
//        {
//            ExampleViewModel[] exampleViewModels = new ExampleViewModel[SynsetModel.Examples.Length];
//            for (int i = 0; i < SynsetModel.Examples.Length; ++i)
//            {
//                exampleViewModels[i] = new ExampleViewModel
//                {
//                    Words = Words.ToArray(),
//                    Example = SynsetModel.Examples[i]
//                };
//            }
//            Examples = exampleViewModels;
//        }
//    }

//    public SynsetModel SynsetModel
//    {
//        get;
//    }

//    public string QueryText
//    {
//        get;
//    }

//    public WordNetSqlite.PartOfSpeech Pos
//    {
//        get;
//    }

//    public uint Key => SynsetModel.Key;

//    public string Definition => SynsetModel.Definition;

//    public string[] Words => SynsetModel.Words;

//    public ExampleViewModel[]? Examples
//    {
//        get;
//    }

//    public uint IndexInQuery
//    {
//        get;
//    }
//}
