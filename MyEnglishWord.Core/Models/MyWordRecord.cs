using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace MyEnglishWord.Core.Models;

[Table("my_word_record")]
[Index(nameof(Word), IsUnique = true)]
[Index(nameof(Time), IsUnique = true)]
public class MyWordRecord
{

    [Key]
    public int Id
    {
        get; set;
    }

    [Column("word", TypeName = "TEXT")]
    [Required]
    public required string Word
    {
        get; set;
    }

    [Column("time", TypeName = "DATETIME")]
    [Required]
    public required DateTime Time
    {
        get; set;
    }
}
