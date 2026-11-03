using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MyEnglishWord.Core.Models;

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
}
