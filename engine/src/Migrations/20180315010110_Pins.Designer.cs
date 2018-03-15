﻿// <auto-generated />
using Ipfs.Engine;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using System;

namespace Ipfs.Engine.Migrations
{
    [DbContext(typeof(Repository))]
    [Migration("20180315010110_Pins")]
    partial class Pins
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.0.1-rtm-125");

            modelBuilder.Entity("Ipfs.Engine.Cryptography.EncryptedKey", b =>
                {
                    b.Property<string>("Name")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Pem");

                    b.HasKey("Name");

                    b.ToTable("EncryptedKeys");
                });

            modelBuilder.Entity("Ipfs.Engine.Cryptography.KeyInfo", b =>
                {
                    b.Property<string>("Name")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("_Id")
                        .HasColumnName("Id");

                    b.HasKey("Name");

                    b.ToTable("Keys");
                });

            modelBuilder.Entity("Ipfs.Engine.Repository+Config", b =>
                {
                    b.Property<string>("Name")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Value");

                    b.HasKey("Name");

                    b.ToTable("Configs");
                });

            modelBuilder.Entity("Ipfs.Engine.Repository+Pin", b =>
                {
                    b.Property<string>("Cid")
                        .ValueGeneratedOnAdd();

                    b.HasKey("Cid");

                    b.ToTable("Pins");
                });
#pragma warning restore 612, 618
        }
    }
}
