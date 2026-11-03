using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MyEnglishWord.Core.Models;
using Windows.Data.Xml.Dom;

namespace MyEnglishWord.Core.DataBase;

public class EnglishWordContext : DbContext
{
    public DbSet<WordEntry> Words
    {
        get; set;
    }

    public EnglishWordContext(DbContextOptions<EnglishWordContext> options)
        : base(options)
    {
    }

    //查询单词
    public Task<WordEntry?> QueryWordAsync(string word)
    {
        return Words.AsNoTracking().SingleOrDefaultAsync(x => x.Word == word);
    }

    //查询单词匹配
    public Task<List<WordEntry>> Match(string word, int limit = 20, bool strip = false)
    {
        var query = Words.AsNoTracking();
        if (!strip)
        {
            query = query
                    .Where(x => EF.Functions.Like(x.Word, $"{word}%") && x.Word != word)
                    .OrderBy(x => EF.Functions.Collate(x.Word, "NOCASE"));
        }
        else
        {
            var nword = StripWord(word);
            query = query
                   .Where(x => EF.Functions.Like(x.Sw, $"{nword}%") && x.Sw != nword)
                   .OrderBy(x => EF.Functions.Collate(x.Sw, "NOCASE"));
        }

        return query.Take(limit)
            .ToListAsync();
    }

    //批量查询单词
    public Task<List<WordEntry>> QueryBatch(params string[] word)
    {
        if (word is null || word.Length == 0)
        {
            return Task.FromResult<List<WordEntry>>([]);
        }

        return Words
            .AsNoTracking()
            .Where(x => word.Contains(x.Word))
            .ToListAsync();
    }

    //取得单词总数
    public Task<int> CountAsync()
    {
        return Words.AsNoTracking().CountAsync();
    }

    //todo: 增加 删除 更新

    private static string StripWord(string? word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return string.Empty;

        return Regex.Replace(
            word.Trim(),
            @"^[^\p{L}\p{N}]+|[^\p{L}\p{N}]+$",
            "");
    }
}
