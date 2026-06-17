using Chronos.Core.Abstractions.Hooks;

namespace Chronos.Core.Abstractions.UnitTests.Hooks;

public class ChromosomeTests
{
    [Fact]
    public void Default_Values()
    {
        var chromosome = new Chromosome { Genes = [1.0, 2.0] };
        // Fitness is a plain double, defaulting to 0.0.
        Assert.Equal(0.0, chromosome.Fitness);
        Assert.Equal(0, chromosome.Generation);
        Assert.Equal(0, chromosome.IndividualIndex);
        Assert.Equal(0, chromosome.Seed);
    }

    [Fact]
    public void With_Values_Stores()
    {
        var chromosome = new Chromosome
        {
            Genes = [3.0],
            Fitness = 0.5,
            Generation = 5,
            IndividualIndex = 10,
            Seed = 42
        };
        Assert.Equal(0.5, chromosome.Fitness);
        Assert.Equal(5, chromosome.Generation);
        Assert.Equal(10, chromosome.IndividualIndex);
        Assert.Equal(42, chromosome.Seed);
    }

    [Fact]
    public void Genes_Can_Be_Empty()
    {
        var chromosome = new Chromosome { Genes = [] };
        Assert.Empty(chromosome.Genes);
        Assert.Equal(0.0, chromosome.Fitness);
    }
}
