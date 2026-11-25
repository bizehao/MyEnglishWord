using Microsoft.EntityFrameworkCore;
using WordNetSqlite.Model;

namespace WordNetSqlite
{
    /// <summary>
    /// EF Core context over a WordNet SQLite database produced by WordNetSqliteDbGen.
    /// </summary>
    public class WordNetContext : DbContext
    {
        public DbSet<LexicographerFile> LexicographerFiles { get; set; } = null!;
        public DbSet<SynsetRecord> Synsets { get; set; } = null!;
        public DbSet<SynsetWord> SynsetWords { get; set; } = null!;
        public DbSet<Pointer> Pointers { get; set; } = null!;
        public DbSet<VerbFrame> VerbFrames { get; set; } = null!;
        public DbSet<IndexEntry> IndexEntries { get; set; } = null!;
        public DbSet<IndexPointerType> IndexPointerTypes { get; set; } = null!;
        public DbSet<IndexSense> IndexSenses { get; set; } = null!;
        public DbSet<SenseIndexEntry> SenseIndexEntries { get; set; } = null!;
        public DbSet<SenseTagCount> SenseTagCounts { get; set; } = null!;
        public DbSet<MorphException> MorphExceptions { get; set; } = null!;
        public DbSet<VerbFrameText> VerbFrameTexts { get; set; } = null!;
        public DbSet<VerbSentenceTemplate> VerbSentenceTemplates { get; set; } = null!;
        public DbSet<VerbSentenceIndex> VerbSentenceIndexes { get; set; } = null!;

        public string DbPath { get; }

        public bool ReadOnly { get; }

        public WordNetContext(string dbPath, bool readOnly = false)
        {
            DbPath = dbPath;
            ReadOnly = readOnly;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            var connectionString = ReadOnly ? $"Data Source={DbPath};Mode=ReadOnly" : $"Data Source={DbPath}";
            _ = optionsBuilder.UseSqlite(connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SynsetRecord>(entity =>
            {
                entity.HasIndex(e => new { e.Pos, e.Offset }).IsUnique();
            });

            modelBuilder.Entity<SynsetWord>(entity =>
            {
                entity.HasIndex(e => new { e.SynsetId, e.WordNumber }).IsUnique();
                entity.HasOne(e => e.Synset!)
                    .WithMany(e => e.Words)
                    .HasForeignKey(e => e.SynsetId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Pointer>(entity =>
            {
                entity.HasIndex(e => new { e.SynsetId, e.Index }).IsUnique();
                entity.HasOne(e => e.Synset!)
                    .WithMany(e => e.Pointers)
                    .HasForeignKey(e => e.SynsetId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.TargetSynset!)
                    .WithMany()
                    .HasForeignKey(e => e.TargetSynsetId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<VerbFrame>(entity =>
            {
                entity.HasIndex(e => new { e.SynsetId, e.Index }).IsUnique();
                entity.HasOne(e => e.Synset!)
                    .WithMany(e => e.Frames)
                    .HasForeignKey(e => e.SynsetId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<IndexEntry>(entity =>
            {
                entity.HasIndex(e => new { e.Lemma, e.Pos }).IsUnique();
            });

            modelBuilder.Entity<IndexPointerType>(entity =>
            {
                entity.HasIndex(e => new { e.IndexEntryId, e.Index }).IsUnique();
                entity.HasOne(e => e.IndexEntry!)
                    .WithMany(e => e.PointerTypes)
                    .HasForeignKey(e => e.IndexEntryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<IndexSense>(entity =>
            {
                entity.HasIndex(e => new { e.IndexEntryId, e.SenseNumber }).IsUnique();
                entity.HasOne(e => e.IndexEntry!)
                    .WithMany(e => e.Senses)
                    .HasForeignKey(e => e.IndexEntryId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Synset!)
                    .WithMany()
                    .HasForeignKey(e => e.SynsetId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SenseIndexEntry>(entity =>
            {
                entity.HasOne(e => e.Synset!)
                    .WithMany()
                    .HasForeignKey(e => e.SynsetId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<MorphException>(entity =>
            {
                entity.HasIndex(e => new { e.Pos, e.Form, e.Index });
            });

            modelBuilder.Entity<VerbFrameText>(entity =>
            {
                entity.HasKey(e => e.FrameNumber);
                entity.Property(e => e.FrameNumber).ValueGeneratedNever();
            });

            modelBuilder.Entity<VerbSentenceTemplate>(entity =>
            {
                entity.HasKey(e => e.TemplateId);
                entity.Property(e => e.TemplateId).ValueGeneratedNever();
            });

            modelBuilder.Entity<VerbSentenceIndex>(entity =>
            {
                entity.HasKey(e => e.SenseKey);
            });

            modelBuilder.Entity<LexicographerFile>(entity =>
            {
                entity.Property(e => e.Id).ValueGeneratedNever();
            });
        }
    }
}
