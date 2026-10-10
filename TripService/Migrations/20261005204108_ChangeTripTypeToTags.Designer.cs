
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using TripService.Data;

#nullable disable

namespace TripService.Migrations
{
    [DbContext(typeof(TripDbContext))]
    [Migration("20261005204108_ChangeTripTypeToTags")]
    partial class ChangeTripTypeToTags
    {
        
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.12")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("TripService.Entities.Trip", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasDefaultValueSql("NOW()");

                    b.Property<DateOnly>("DepartDate")
                        .HasColumnType("date");

                    b.Property<DateOnly>("ReturnDate")
                        .HasColumnType("date");

                    b.PrimitiveCollection<List<string>>("Tags")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("trips", (string)null);
                });

            modelBuilder.Entity("TripService.Entities.Trip", b =>
                {
                    b.OwnsOne("TripService.Entities.Destination", "Destination", b1 =>
                        {
                            b1.Property<Guid>("TripId");

                            b1.Property<string>("BannerImage");

                            b1.Property<string>("City")
                                .IsRequired();

                            b1.Property<string>("Country")
                                .IsRequired();

                            b1.Property<string>("CountryFlag")
                                .IsRequired();

                            b1.Property<string>("CurrentSeason");

                            b1.Property<string>("WeatherToday");

                            b1.HasKey("TripId");

                            b1.ToTable("trips");

                            b1
                                .ToJson("Destination")
                                .HasColumnType("jsonb");

                            b1.WithOwner()
                                .HasForeignKey("TripId");
                        });

                    b.Navigation("Destination")
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
