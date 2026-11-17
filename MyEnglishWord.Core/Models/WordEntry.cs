using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace MyEnglishWord.Core.Models;

[Table("stardict")]
public sealed class WordEntry
{
    [Column("id")]
    public long Id
    {
        get; set;
    }


    //单词名称
    [Column("word")]
    public required string Word
    {
        get; set;
    }

    [Column("sw")]
    public required string Sw
    {
        get; set;
    }

    //	音标，以英语英标为主
    [Column("phonetic")]
    public string? Phonetic
    {
        get; set;
    }

    //单词释义（英文），每行一个释义
    [Column("definition")]
    public string? Definition
    {
        get; set;
    }

    //单词释义（中文），每行一个释义
    [Column("translation")]
    public string? Translation
    {
        get; set;
    }

    //词语位置，用 "/" 分割不同位置
    [Column("pos")]
    public string? Pos
    {
        get; set;
    }

    //柯林斯星级
    [Column("collins")]
    public int Collins { get; set; } = 0;

    //是否是牛津三千核心词汇
    [Column("oxford")]
    public int? Oxford { get; set; } = 0;

    //字符串标签：zk/中考，gk/高考，cet4/四级 等等标签，空格分割
    [Column("tag")]
    public string? Tag
    {
        get; set;
    }

    //英国国家语料库词频顺序
    [Column("bnc")]
    public int Bnc
    {
        get; set;
    }

    //当代语料库词频顺序
    [Column("frq")]
    public int Frq
    {
        get; set;
    }

    [Column("exchange")]
    public string? Exchange
    {
        get; set;
    }

    [Column("detail")]
    public string? Detail
    {
        get; set;
    }

    [Column("audio")]
    public string? Audio
    {
        get; set;
    }
}