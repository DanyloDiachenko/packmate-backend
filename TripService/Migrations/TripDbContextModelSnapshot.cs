using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using TripService.Data;

#nullable disable

namespace TripService.Migrations
{
    [DbContext(typeof(TripDbContext))]
    partial class TripDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
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

                    b.Property<bool>("HasWeatherImpact")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsArchived")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<DateTime?>("LastWeatherCheckAt")
                        .HasColumnType("timestamp with time zone");

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

            modelBuilder.Entity("TripService.Entities.TripWeatherForecastCache", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Condition")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<DateTime>("FetchedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasDefaultValueSql("NOW()");

                    b.Property<DateOnly>("ForecastDate")
                        .HasColumnType("date");

                    b.Property<int>("HighTemp")
                        .HasColumnType("integer");

                    b.Property<string>("IconName")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<int>("LowTemp")
                        .HasColumnType("integer");

                    b.Property<int>("RainProbability")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0);

                    b.Property<Guid>("TripId")
                        .HasColumnType("uuid");

                    b.Property<int>("UvIndex")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0);

                    b.Property<int>("WindSpeed")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0);

                    b.HasKey("Id");

                    b.HasIndex("TripId", "ForecastDate")
                        .IsUnique();

                    b.ToTable("trip_weather_forecasts", (string)null);
                });

            modelBuilder.Entity("TripService.Entities.TripWeatherImpact", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("DetectedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasDefaultValueSql("NOW()");

                    b.Property<string>("ExistingMatchedNotice")
                        .HasColumnType("text");

                    b.Property<string>("Highlights")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<bool>("IsResolved")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false);

                    b.Property<string>("Proposals")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<DateTime?>("ResolvedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("TripId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("TripId");

                    b.ToTable("trip_weather_impacts", (string)null);
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

            modelBuilder.Entity("TripService.Entities.TripWeatherForecastCache", b =>
                {
                    b.HasOne("TripService.Entities.Trip", "Trip")
                        .WithMany()
                        .HasForeignKey("TripId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Trip");
                });

            modelBuilder.Entity("TripService.Entities.TripWeatherImpact", b =>
                {
                    b.HasOne("TripService.Entities.Trip", "Trip")
                        .WithMany()
                        .HasForeignKey("TripId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Trip");
                });
#pragma warning restore 612, 618
        }
    }
}
