namespace EmergencyVoucherManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Distribution : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Distributions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Title = c.String(),
                        Date = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.DistributionVoucherCategories",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TypeId = c.Int(nullable: false),
                        DistributionId = c.Int(nullable: false),
                        LocationId = c.Int(nullable: false),
                        NumberOfVouchers = c.Int(nullable: false),
                        Value = c.Decimal(precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Distributions", t => t.DistributionId, cascadeDelete: true)
                .ForeignKey("dbo.Locations", t => t.LocationId, cascadeDelete: true)
                .ForeignKey("dbo.VoucherTypes", t => t.TypeId, cascadeDelete: true)
                .Index(t => t.TypeId)
                .Index(t => t.DistributionId)
                .Index(t => t.LocationId);
            
            CreateTable(
                "dbo.Locations",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.VoucherTypes",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            AddColumn("dbo.Vouchers", "TypeId", c => c.Int(nullable: false));
            AddColumn("dbo.Vouchers", "DistributionId", c => c.Int(nullable: false));
            CreateIndex("dbo.Vouchers", "TypeId");
            CreateIndex("dbo.Vouchers", "DistributionId");
            AddForeignKey("dbo.Vouchers", "DistributionId", "dbo.Distributions", "Id", cascadeDelete: true);
            AddForeignKey("dbo.Vouchers", "TypeId", "dbo.VoucherTypes", "Id", cascadeDelete: true);
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Vouchers", "TypeId", "dbo.VoucherTypes");
            DropForeignKey("dbo.Vouchers", "DistributionId", "dbo.Distributions");
            DropForeignKey("dbo.DistributionVoucherCategories", "TypeId", "dbo.VoucherTypes");
            DropForeignKey("dbo.DistributionVoucherCategories", "LocationId", "dbo.Locations");
            DropForeignKey("dbo.DistributionVoucherCategories", "DistributionId", "dbo.Distributions");
            DropIndex("dbo.Vouchers", new[] { "DistributionId" });
            DropIndex("dbo.Vouchers", new[] { "TypeId" });
            DropIndex("dbo.DistributionVoucherCategories", new[] { "LocationId" });
            DropIndex("dbo.DistributionVoucherCategories", new[] { "DistributionId" });
            DropIndex("dbo.DistributionVoucherCategories", new[] { "TypeId" });
            DropColumn("dbo.Vouchers", "DistributionId");
            DropColumn("dbo.Vouchers", "TypeId");
            DropTable("dbo.VoucherTypes");
            DropTable("dbo.Locations");
            DropTable("dbo.DistributionVoucherCategories");
            DropTable("dbo.Distributions");
        }
    }
}
