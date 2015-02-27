namespace EmergencyVoucherManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Beneficiaries",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                        NationalId = c.String(),
                        PIN = c.String(),
                        MobileNumber = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Vendors",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                        OwnerName = c.String(),
                        MobileNumber = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Vouchers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Value = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.VoucherVerificationItems",
                c => new
                    {
                        Id = c.Int(nullable: false),
                        VoucherId = c.Int(nullable: false),
                        BeneficiaryId = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Beneficiaries", t => t.BeneficiaryId, cascadeDelete: true)
                .ForeignKey("dbo.Vouchers", t => t.Id)
                .Index(t => t.Id)
                .Index(t => t.BeneficiaryId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.VoucherVerificationItems", "Id", "dbo.Vouchers");
            DropForeignKey("dbo.VoucherVerificationItems", "BeneficiaryId", "dbo.Beneficiaries");
            DropIndex("dbo.VoucherVerificationItems", new[] { "BeneficiaryId" });
            DropIndex("dbo.VoucherVerificationItems", new[] { "Id" });
            DropTable("dbo.VoucherVerificationItems");
            DropTable("dbo.Vouchers");
            DropTable("dbo.Vendors");
            DropTable("dbo.Beneficiaries");
        }
    }
}
