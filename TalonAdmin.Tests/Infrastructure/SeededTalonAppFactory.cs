namespace TalonAdmin.Tests.Infrastructure;

/// <summary>Application with the sample programme seeded, for tests that only need data to query.</summary>
public class SeededTalonAppFactory : TalonAppFactory
{
    protected override bool SeedDemoData => true;
}
