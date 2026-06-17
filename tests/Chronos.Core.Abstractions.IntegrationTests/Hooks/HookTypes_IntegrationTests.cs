using Chronos.Core.Abstractions.Hooks;

namespace Chronos.Core.Abstractions.IntegrationTests.Hooks;

public class HookTypes_IntegrationTests
{
    [Fact]
    public void Chromosome_Large_Gene_Array()
    {
        var genes = new double[1000];
        for (int i = 0; i < genes.Length; i++)
        {
            genes[i] = i * 0.01;
        }

        var chromosome = new Chromosome
        {
            Genes = genes,
            Fitness = 0.85,
            Generation = 42,
            IndividualIndex = 100,
            Seed = 12345
        };

        Assert.Equal(1000, chromosome.Genes.Length);
        Assert.Equal(0.85, chromosome.Fitness);
        Assert.Equal(42, chromosome.Generation);
        Assert.Equal(100, chromosome.IndividualIndex);
        Assert.Equal(12345, chromosome.Seed);
    }

    [Fact]
    public void Chromosome_Default_Fitness_Is_Zero()
    {
        var chromosome = new Chromosome { Genes = [1.0, 2.0, 3.0] };
        Assert.Equal(0.0, chromosome.Fitness);
        Assert.Equal(0, chromosome.Generation);
    }

    [Fact]
    public void EquitySnapshot_All_Values()
    {
        var snapshot = new EquitySnapshot(10000, 9500, 5.0, 2.5);
        Assert.Equal(10000, snapshot.Equity);
        Assert.Equal(9500, snapshot.Balance);
        Assert.Equal(5.0, snapshot.Drawdown);
        Assert.Equal(2.5, snapshot.DailyDrawdown);

        var zero = new EquitySnapshot(0, 0, 0, 0);
        Assert.Equal(0, zero.Equity);
    }
}
