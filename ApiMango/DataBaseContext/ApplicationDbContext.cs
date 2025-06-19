using ApiMango.Model;
using Microsoft.EntityFrameworkCore;

namespace ApiMango.DataBaseContext
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<LevelProgress> LevelProgresses { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LevelProgress>()
                .HasOne(lp => lp.User)
                .WithMany(u => u.LevelProgresses)
                .HasForeignKey(lp => lp.UserId);

            base.OnModelCreating(modelBuilder);
        }
    }
}