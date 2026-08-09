using MigrationLint.Core.Model;
using MigrationLint.Core.Parsing;
using Xunit;

namespace MigrationLint.Tests;

public class ParserTests
{
    [Fact]
    public void ParsesAllFixturesWithoutThrowing()
    {
        foreach (var path in TestHarness.AllFixtures())
        {
            var ex = Record.Exception(() =>
                MigrationFileParser.TryParse(path, File.ReadAllText(path), out _, out _));
            Assert.Null(ex);
        }
    }

    [Fact]
    public void ReadsAddColumnShape()
    {
        var m = TestHarness.Parse("Bad_AddNotNullNoDefault");
        var op = Assert.Single(m.UpOperations, o => o.Kind == OperationKind.AddColumn);

        Assert.Equal("Orders", op.Table);
        Assert.Equal("Notes", op.Name);
        Assert.Equal("string", op.Column!.ClrType);
        Assert.False(op.Column.IsNullable);
        Assert.False(op.Column.HasDefault);
    }

    [Fact]
    public void SourceLineMatchesActualLine()
    {
        var m = TestHarness.Parse("Bad_CreateIndexNoConcurrently");
        var op = Assert.Single(m.UpOperations);

        // The CreateIndex call begins on line 9 in the fixture.
        Assert.Equal(9, op.Source.Line);
    }

    [Fact]
    public void NonLiteralArgumentsParseWithoutThrowingAndRecordUnknown()
    {
        var m = TestHarness.Parse("Edge_NonLiteralArguments");
        var op = Assert.Single(m.UpOperations, o => o.Kind == OperationKind.AddColumn);

        // nullable: someVar and maxLength: 10*5 are non-literal → unknown, not a guess.
        Assert.Null(op.Column!.IsNullable);
        Assert.Null(op.Column.MaxLength);
    }

    [Fact]
    public void PositionalArgumentsResolve()
    {
        var m = TestHarness.Parse("Edge_PositionalArguments");
        var op = Assert.Single(m.UpOperations, o => o.Kind == OperationKind.DropColumn);

        Assert.Equal("LegacyFlag", op.Name);
        Assert.Equal("Orders", op.Table);
    }

    [Fact]
    public void CollectsChainedAnnotations()
    {
        var m = TestHarness.Parse("Good_CreateIndexConcurrently");
        var op = Assert.Single(m.UpOperations, o => o.Kind == OperationKind.CreateIndex);

        Assert.True(op.Annotations.ContainsKey("Npgsql:CreatedConcurrently"));
        Assert.Equal("true", op.Annotations["Npgsql:CreatedConcurrently"]);
    }

    [Fact]
    public void ReadsAlterColumnOldAndNewValues()
    {
        var m = TestHarness.Parse("Bad_NarrowType");
        var op = Assert.Single(m.UpOperations, o => o.Kind == OperationKind.AlterColumn);

        Assert.Equal(50, op.Column!.MaxLength);
        Assert.Equal(200, op.OldColumn!.MaxLength);
    }
}
