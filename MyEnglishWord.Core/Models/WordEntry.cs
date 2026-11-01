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

    [Column("phonetic")]
    public string? Phonetic
    {
        get; set;
    }
    [Column("definition")]
    public string? Definition
    {
        get; set;
    }
    [Column("translation")]
    public string? Translation
    {
        get; set;
    }
    [Column("pos")]
    public string? Pos
    {
        get; set;
    }

    [Column("collins")]
    public int? Collins { get; set; } = 0;

    [Column("oxford")]
    public int? Oxford { get; set; } = 0;

    [Column("tag")]
    public string? Tag
    {
        get; set;
    }

    [Column("bnc")]
    public int? Bnc
    {
        get; set;
    }

    [Column("frq")]
    public int? Frq
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