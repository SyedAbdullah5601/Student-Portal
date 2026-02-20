using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace StudentPortal.Models;

public partial class StudentLoginContext : DbContext
{
    public StudentLoginContext()
    {
    }

    public StudentLoginContext(DbContextOptions<StudentLoginContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Attendance> Attendances { get; set; }

    public virtual DbSet<Candidate> Candidates { get; set; }

    public virtual DbSet<Course> Courses { get; set; }

    public virtual DbSet<CourseAssignment> CourseAssignments { get; set; }

    public virtual DbSet<Enrollment> Enrollments { get; set; }

    public virtual DbSet<Menu> Menus { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<RoleMenuMapping> RoleMenuMappings { get; set; }

    public virtual DbSet<SystemLog> SystemLogs { get; set; }

    public virtual DbSet<UserType> UserTypes { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=localhost\\SQLEXPRESS;Database=StudentLogin;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasKey(e => e.AttendanceId).HasName("PK__Attendan__8B69261CDE5722AF");

            entity.ToTable("Attendance");

            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Unmarked");

            entity.HasOne(d => d.Enrollment).WithMany(p => p.Attendances)
                .HasForeignKey(d => d.EnrollmentId)
                .HasConstraintName("FK__Attendanc__Enrol__2E1BDC42");
        });

        modelBuilder.Entity<Candidate>(entity =>
        {
            entity.HasKey(e => e.CandidateId).HasName("PK__Candidat__DF539B9C1306C74D");

            entity.HasIndex(e => e.Username, "UQ__Candidat__536C85E49D9F84C5").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FirstName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(50);
            entity.Property(e => e.OtpCode).HasMaxLength(6);
            entity.Property(e => e.OtpExpiry).HasColumnType("datetime");
            entity.Property(e => e.PhoneNo).HasMaxLength(20);
            entity.Property(e => e.ProfilePicturePath).HasMaxLength(500);
            entity.Property(e => e.UserTypeId).HasDefaultValue(1);
            entity.Property(e => e.Username).HasMaxLength(30);

            entity.HasOne(d => d.UserType).WithMany(p => p.Candidates)
                .HasForeignKey(d => d.UserTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Candidates_UserTypes");
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(e => e.CourseId).HasName("PK__Courses__C92D71A7C4038B26");

            entity.HasIndex(e => e.CourseCode, "UQ__Courses__FC00E000074C98BE").IsUnique();

            entity.Property(e => e.CourseCode).HasMaxLength(20);
            entity.Property(e => e.CourseName).HasMaxLength(200);
        });

        modelBuilder.Entity<CourseAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("PK__CourseAs__32499E773CA9DE0E");

            entity.Property(e => e.SectionName).HasMaxLength(50);

            entity.HasOne(d => d.Course).WithMany(p => p.CourseAssignments)
                .HasForeignKey(d => d.CourseId)
                .HasConstraintName("FK__CourseAss__Cours__24927208");

            entity.HasOne(d => d.Faculty).WithMany(p => p.CourseAssignments)
                .HasForeignKey(d => d.FacultyId)
                .HasConstraintName("FK__CourseAss__Facul__25869641");
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.HasKey(e => e.EnrollmentId).HasName("PK__Enrollme__7F68771B537538D3");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Mark).HasDefaultValue(0);

            entity.HasOne(d => d.Assignment).WithMany(p => p.Enrollments)
                .HasForeignKey(d => d.AssignmentId)
                .HasConstraintName("FK__Enrollmen__Assig__286302EC");

            entity.HasOne(d => d.Student).WithMany(p => p.Enrollments)
                .HasForeignKey(d => d.StudentId)
                .HasConstraintName("FK__Enrollmen__Stude__29572725");
        });

        modelBuilder.Entity<Menu>(entity =>
        {
            entity.HasKey(e => e.MenuId).HasName("PK__Menus__C99ED230CA023CC5");

            entity.Property(e => e.Icon).HasMaxLength(50);
            entity.Property(e => e.MenuName).HasMaxLength(100);
            entity.Property(e => e.Url).HasMaxLength(200);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotifId).HasName("PK__Notifica__DDBFF3334E19C416");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.TargetUserType).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.TargetUserTypeId)
                .HasConstraintName("FK__Notificat__Targe__32E0915F");
        });

        modelBuilder.Entity<RoleMenuMapping>(entity =>
        {
            entity.HasKey(e => e.MappingId).HasName("PK__RoleMenu__8B57819DED911822");

            entity.ToTable("RoleMenuMapping");

            entity.HasOne(d => d.Menu).WithMany(p => p.RoleMenuMappings)
                .HasForeignKey(d => d.MenuId)
                .HasConstraintName("FK__RoleMenuM__MenuI__1ED998B2");

            entity.HasOne(d => d.UserType).WithMany(p => p.RoleMenuMappings)
                .HasForeignKey(d => d.UserTypeId)
                .HasConstraintName("FK__RoleMenuM__UserT__1DE57479");
        });

        modelBuilder.Entity<SystemLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__SystemLo__5E5486485D716731");

            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.ActionName).HasMaxLength(50);
            entity.Property(e => e.ControllerName).HasMaxLength(50);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.Timestamp)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UserRole).HasMaxLength(20);

            entity.HasOne(d => d.User).WithMany(p => p.SystemLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Logs_Candidates");
        });

        modelBuilder.Entity<UserType>(entity =>
        {
            // Use the name from your SQL Script
            entity.HasKey(e => e.UserTypeId).HasName("PK__UserType__40D2D8161F35C370");

            // This is the critical change:
            // It tells EF that the DB handles the ID and EF should NEVER send it.
            entity.Property(e => e.UserTypeId)
                  .UseIdentityColumn()                // Specific to SQL Server
                  .ValueGeneratedOnAdd();             // Ensures EF doesn't send 0

            entity.Property(e => e.Prefix)
                  .HasMaxLength(10)
                  .IsRequired(false);

            entity.Property(e => e.TypeName)
                  .HasMaxLength(20);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
