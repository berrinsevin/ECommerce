using System;
using System.Linq;
using System.Threading;
using Ardalis.GuardClauses;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Intertech.MtmAutomationNuget.Logs;
using Intertech.MtmAutomationNuget.Utils;
using Intertech.MtmAutomationNuget.Entity;
using Intertech.MtmAutomationNuget.Entity.Job;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Intertech.MtmAutomationNuget.Entity.Definition;
using Intertech.MtmAutomationNuget.Entity.Transaction;

namespace Intertech.MtmAutomation.Context
{
    /// <summary>
    /// Db accesss layer 
    /// </summary>
    public class MtmDbContext : DbContext
    {
        /// <summary>
        /// builder ctor
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public MtmDbContext(DbContextOptions options) : base(options)
        {
        }
        /// <summary>
        /// RecordStatus for query filtering
        /// </summary>
        public string RecordStatus { get; set; }

        /// <summary>
        /// Datasources
        /// </summary>
        public DbSet<DataSource> DataSource { get; set; }
        /// <summary>
        /// DataSourceDictionary
        /// </summary>
        public DbSet<DataSourceDictionary> DataSourceDictionary { get; set; }
        /// <summary>
        /// FileDefinition
        /// </summary>
        public DbSet<FileDefinition> FileDefinition { get; set; }
        /// <summary>
        /// FileDataSource
        /// </summary>
        public DbSet<FileDataSource> FileDataSource { get; set; }
        /// <summary>
        /// FieldMapping
        /// </summary>
        public DbSet<FieldMapping> FieldMapping { get; set; }
        /// <summary>
        /// FileDataSource
        /// </summary>
        public DbSet<FileExecutionHour> FileExecutionHour { get; set; }
        /// <summary>
        /// Execution
        /// </summary>
        public DbSet<Execution> Execution { get; set; }
        /// <summary>
        /// GeneratedFile
        /// </summary>
        public DbSet<GeneratedFile> GeneratedFile { get; set; }
        /// <summary>
        /// FileTemplate
        /// </summary>
        public DbSet<FileTemplate> FileTemplate { get; set; }
        /// <summary>
        /// ExecutionDataItemCount
        /// </summary>
        public DbSet<ExecutionDataItemCount> ExecutionDataItemCount { get; set; }
        /// <summary>
        /// FxTransaction
        /// </summary>
        public DbSet<FxTransaction> FxTransaction { get; set; }
        /// <summary>
        /// TransactionResponseLog
        /// </summary>
        public DbSet<TransactionResponse> TransactionResponse { get; set; }
        /// <summary>
        /// FileRelation
        /// </summary>
        public DbSet<FileRelation> FileRelation { get; set; }

        /// <summary>
        /// saves changes
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SetCommonProperties();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void SetCommonProperties()
        {
            var entries = ChangeTracker.Entries().Where(e => e.Entity is BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                var entity = (BaseEntity)entry.Entity;

                entity = SetCreatedProperties(entity, entry);
                entity = SetLastUpdatedProperties(entity);

                if (string.IsNullOrEmpty(entity.RecordStatus))
                {
                    entity.RecordStatus = "A";
                }
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            Guard.Against.Null(modelBuilder);

            modelBuilder.Entity<Execution>().ToTable("Execution", "JOB");
            modelBuilder.Entity<FileDefinition>().ToTable("FileDefinition", "DEF");
            modelBuilder.Entity<FileDataSource>().ToTable("FileDataSource", "DEF");
            modelBuilder.Entity<DataSourceDictionary>().ToTable("DataSourceDictionary", "DEF");
            modelBuilder.Entity<FileTemplate>().ToTable("FileTemplate", "DEF");
            modelBuilder.Entity<FileExecutionHour>().ToTable("FileExecutionHour", "DEF");
            modelBuilder.Entity<FieldMapping>().ToTable("FieldMapping", "DEF");
            modelBuilder.Entity<GeneratedFile>().ToTable("GeneratedFile", "FLE");
            modelBuilder.Entity<DataSource>().ToTable("DataSource", "DEF");
            modelBuilder.Entity<ExecutionDataItemCount>().ToTable("ExecutionDataItemCount", "TRN");
            modelBuilder.Entity<FxTransaction>().ToTable("FxTransaction", "TRN");
            modelBuilder.Entity<TransactionResponse>().ToTable("TransactionResponse", "TRN");
            modelBuilder.Entity<SourceRelation>().ToTable("SourceRelation", "DEF");
            modelBuilder.Entity<FileDataSourceTemplate>().ToTable("FileDataSourceTemplate", "DEF");
            modelBuilder.Entity<FileRelation>().ToTable("FileRelation", "DEF");

            modelBuilder.Entity<Execution>().HasOne(x => x.FileDefinition).WithMany(t => t.Executions).HasForeignKey(y => y.FileDefId);
            modelBuilder.Entity<Execution>().HasOne(x => x.GeneratedFile);
            modelBuilder.Entity<Execution>().HasOne(x => x.LogFile);

            modelBuilder.Entity<FileDefinition>().HasMany(x => x.FileDataSources).WithOne(x => x.FileDefinition).HasForeignKey(x => x.FileDefId);
            modelBuilder.Entity<FileDefinition>().HasMany(x => x.FileTemplates).WithOne(x => x.FileDefinition).HasForeignKey(x => x.FileDefId);
            modelBuilder.Entity<FileDefinition>().HasMany(x => x.FileRelations).WithOne(x => x.SourceFileDefinition).HasForeignKey(x => x.SourceFileDefId);

            modelBuilder.Entity<FileDefinition>().HasMany(x => x.ExecutionHours).WithOne(x => x.FileDefinition).HasForeignKey(x => x.FileDefId);

            modelBuilder.Entity<DataSource>().HasMany(x => x.DataSourceDictionary).WithOne(x => x.DataSource).HasForeignKey(x => x.DataSourceId);

            modelBuilder.Entity<FileDataSource>().HasOne(x => x.DataSource);
            modelBuilder.Entity<FileDataSource>().HasOne(x => x.FileDefinition);
            modelBuilder.Entity<FileDataSource>().HasMany(x => x.SourceRelations).WithOne(x => x.FileDataSource).HasForeignKey(x => x.FileDataSourceId);
            modelBuilder.Entity<FileDataSource>().HasMany(x => x.FileDataSourceTemplates).WithOne(x => x.FileDataSource).HasForeignKey(x => x.FileDataSourceId);
            modelBuilder.Entity<FileDataSource>().HasMany(x => x.FieldMappings).WithOne(x => x.FileDataSource).HasForeignKey(x => x.FileDataSourceId);

            modelBuilder.Entity<TransactionResponse>().HasOne(x => x.FileDataSource).WithMany(x => x.TransactionResponses).HasForeignKey(x => x.FileDataSourceId);

            modelBuilder.Entity<TransactionResponse>().Property(x => x.CustomId).HasMaxLength(Constants.SEVENTY);
            modelBuilder.Entity<TransactionResponse>().Property(x => x.Message).HasMaxLength(Constants.TWO_HUNDRED_FIFTY);

            modelBuilder.Entity<FxTransaction>().Property(x => x.Name).HasMaxLength(Constants.ONE_HUNDRED);
            modelBuilder.Entity<FxTransaction>().Property(x => x.Value).HasMaxLength(Constants.FIVE_HUNDRED);
            SetQueryFilters(modelBuilder);
        }

        private void SetQueryFilters(ModelBuilder modelBuilder)
        {
            if (!string.IsNullOrEmpty(RecordStatus))
            {
                modelBuilder.Entity<Execution>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
                modelBuilder.Entity<FileDefinition>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
                modelBuilder.Entity<FileDataSource>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
                modelBuilder.Entity<DataSourceDictionary>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
                modelBuilder.Entity<FileTemplate>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
                modelBuilder.Entity<FileExecutionHour>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
                modelBuilder.Entity<FieldMapping>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
                modelBuilder.Entity<GeneratedFile>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
                modelBuilder.Entity<DataSource>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
                modelBuilder.Entity<ExecutionDataItemCount>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
                modelBuilder.Entity<FxTransaction>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
                modelBuilder.Entity<TransactionResponse>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
                modelBuilder.Entity<SourceRelation>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
                modelBuilder.Entity<FileDataSourceTemplate>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
                modelBuilder.Entity<FileRelation>().HasQueryFilter(x => x.RecordStatus == RecordStatus);
            }
        }

        private static BaseEntity SetCreatedProperties(BaseEntity entity, EntityEntry entry)
        {
            if (entry.State == EntityState.Added)
            {
                entity.CreatedDate = DateTime.Now;
            }
            if (string.IsNullOrEmpty(entity.CreatedChannelCode))
            {
                entity.CreatedChannelCode = Constants.BATCH;
            }
            if (string.IsNullOrEmpty(entity.CreatedTranCode))
            {
                entity.CreatedTranCode = Constants.BATCH;
            }
            if (string.IsNullOrEmpty(entity.CreatedUserCode))
            {
                entity.CreatedUserCode = Constants.BATCH;
            }

            return entity;
        }

        private static BaseEntity SetLastUpdatedProperties(BaseEntity entity)
        {
            entity.LastUpdatedDate = DateTime.Now;

            if (string.IsNullOrEmpty(entity.LastUpdatedChannelCode))
            {
                entity.LastUpdatedChannelCode = Constants.BATCH;
            }
            if (string.IsNullOrEmpty(entity.LastUpdatedTranCode))
            {
                entity.LastUpdatedTranCode = Constants.BATCH;
            }
            if (string.IsNullOrEmpty(entity.LastUpdatedUserCode))
            {
                entity.LastUpdatedUserCode = Constants.BATCH;
            }

            return entity;
        }
    }
}
