using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using Microsoft.EntityFrameworkCore;
using MyEnglishWord.Core.Models;
using Windows.UI;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace MyEnglishWord.Core.DataBase;

public class MyWordRecordContext : DbContext
{

    public DbSet<MyWordRecord> WordRecodes
    {
        get; set;
    }


    public MyWordRecordContext(DbContextOptions<MyWordRecordContext> options)
        : base(options)
    {

    }

    //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //{
    //    optionsBuilder.UseSqlite($"Data Source={_dbPath}");
    //}

    public async Task Add(string word)
    {
        await WordRecodes.AddAsync(new MyWordRecord
        {
            Word = word,
            Time = DateTime.Now
        });
        await SaveChangesAsync();
    }

    public Task<bool> Has(string word)
    {
        return WordRecodes.AnyAsync(x => x.Word == word);
    }

    public async Task<List<string>> Has(List<string> word)
    {
        var existWords = await WordRecodes
                        .Where(x => word.Contains(x.Word))
                        .Select(x => x.Word)
                        .ToListAsync();
        return existWords;
    }

    public async Task<(List<MyWordRecord> Records, int PageCount)> GetData(int pageIndex, int pageSize)
    {
        pageIndex += 1;
        var query = WordRecodes
                    .AsNoTracking()
                    .OrderByDescending(x => x.Id) // 一定要排序！主键/唯一字段最稳
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize);
        var data = await query.ToListAsync();

        var totalCount = await WordRecodes.CountAsync();
        var pageCount = (totalCount + pageSize - 1) / pageSize;
        return (data, pageCount);
    }

    public async Task<(List<MyWordRecord> Records, int PageCount)> GetDataByStr(string word, int pageIndex, int pageSize)
    {
        pageIndex += 1;
        var query = WordRecodes
                    .AsNoTracking()
                    .Where(x => x.Word.Contains(word))
                    .OrderByDescending(x => x.Id) // 一定要排序！主键/唯一字段最稳
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize);
        var data = await query.ToListAsync();

        var totalCount = await WordRecodes.CountAsync(x => x.Word.Contains(word));
        var pageCount = (totalCount + pageSize - 1) / pageSize;
        return (data, pageCount);
    }

    public async Task<int> GetTotalCount()
    {
        var totalCount = await WordRecodes.CountAsync();
        return totalCount;
    }

    public async Task Delete(string word)
    {
        await WordRecodes.Where(x => x.Word == word).ExecuteDeleteAsync();
        await SaveChangesAsync();
    }

    public async Task<List<WordCountData>> GetWordCount(DateTime begin, DateTime end, WordCountDataGroupType type)
    {
        var query = WordRecodes.AsQueryable();
        if (type == WordCountDataGroupType.Year)
        {
            begin = new DateTime(begin.Year, 1, 1);
            end = new DateTime(end.Year + 1, 1, 1);
            return await query
                   .Where(x => x.Time >= begin && x.Time < end)
                   .GroupBy(x => x.Time.Year)
                   .Select(g => new
                   {
                       year = g.Key,
                       month = 1,
                       day = 1,
                       count = g.Count()
                   })
                   .OrderBy(x => x.year)
                   .Select(x => new WordCountData(new DateTime(x.year, x.month, x.day), x.count))
                   .ToListAsync();

        }
        else if (type == WordCountDataGroupType.Month)
        {
            begin = new DateTime(begin.Year, begin.Month, 1);
            end = new DateTime(end.Year, end.Month, 1).AddMonths(1);
            return await query
                .Where(x => x.Time >= begin && x.Time < end)
                .GroupBy(x => x.Time.Year * 100 + x.Time.Month)
                .Select(g => new
                {
                    year = g.Key / 100,
                    month = g.Key % 100,
                    day = 1,
                    count = g.Count()
                })
                .OrderBy(x => x.year)
                .Select(x => new WordCountData(new DateTime(x.year, x.month, x.day), x.count))
                .ToListAsync();

        }
        else if (type == WordCountDataGroupType.Day)
        {
            end = end.AddDays(1);
            return await query
                .Where(x => x.Time >= begin && x.Time < end)
                .GroupBy(x => x.Time.Year * 10000 + x.Time.Month * 100 + x.Time.Day)
                .Select(g => new
                {
                    year = g.Key / 10000,
                    month = (g.Key / 100) % 100 == 0 ? 1 : (g.Key / 100) % 100,
                    day = g.Key % 100 == 0 ? 1 : g.Key % 100,
                    count = g.Count()
                })
                .OrderBy(x => x.year)
                .Select(x => new WordCountData(new DateTime(x.year, x.month, x.day), x.count))
                .ToListAsync();

        }
        else
        {
            throw new NotImplementedException();
        }
    }
}
