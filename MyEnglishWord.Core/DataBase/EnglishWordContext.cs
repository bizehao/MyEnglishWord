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
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MyEnglishWord.Core.Models;
using Windows.Data.Xml.Dom;
using Windows.UI;

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

    //批量查询单词
    public async Task<(List<WordEntry> Data,int PageCount)> GetData(int pageIndex, int pageSize)
    {
        pageIndex += 1;
        var query = Words
                    .AsNoTracking()
                    .OrderBy(x => x.Frq + x.Bnc)
                    .ThenByDescending(x => x.Collins)
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize);
        var data = await query.ToListAsync();

        var totalCount = await Words
                    .AsNoTracking()
                    .CountAsync();
        var pageCount = (totalCount + pageSize - 1) / pageSize;
        return (data, pageCount);
    }

    public async Task<(List<WordEntry> Data, int PageCount)> GetDataByStr(string word, int pageIndex, int pageSize)
    {
        pageIndex += 1;
        var query = Words
                    .AsNoTracking()
                    .Where(x => x.Word.StartsWith(word))
                    .OrderBy(x => x.Frq + x.Bnc)
                    .ThenByDescending(x => x.Collins)
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize);
        var data = await query.ToListAsync();

        var totalCount = await Words.AsNoTracking().CountAsync(x => x.Word.StartsWith(word));
        var pageCount = (totalCount + pageSize - 1) / pageSize;
        return (data, pageCount);
    }

    //取得单词总数
    public Task<int> CountAsync()
    {
        return Words.AsNoTracking().CountAsync();
    }

    //todo: 增加 删除 更新

    public async Task<bool> Insert(WordEntry entry)
    {
        Words.Add(entry);
        var v = await SaveChangesAsync();
        return v != 0;
    }

    public async Task<bool> Update(WordEntry entry)
    {
        Words.Update(entry);
        var v = await SaveChangesAsync();
        return v != 0;
    }


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
