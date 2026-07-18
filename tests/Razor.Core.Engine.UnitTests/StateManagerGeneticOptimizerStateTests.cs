namespace Razor.Core.Engine.UnitTests;

using Razor.Core.Engine.Core;
using Razor.Core.Kernel.Optimization;

public class StateManagerGeneticOptimizerStateTests
{
    [Fact]
    public async Task SaveAndLoadOptimizationState_RoundTripsGeneticOptimizerState()
    {
        var stateManager = new StateManager();
        var optimizationId = $"opt_{Guid.NewGuid():N}";
        var population = new GeneticOptimizerState
        {
            Population = Array.Empty<Chromosome>(),
            CurrentGeneration = 5,
            Evaluated = true,
            BestOverallFitness = 0.95,
            StagnationCount = 2,
            HyperMutation = true,
            NeuralNetworkState = null
        };
        var state = new OptimizationState
        {
            TaskId = optimizationId,
            Config = new object(),
            Population = population,
            CurrentGeneration = 5,
            BestFitness = 0.95,
            StartTime = DateTime.UtcNow
        };

        await stateManager.SaveOptimizationStateAsync(optimizationId, state, CancellationToken.None);

        var loaded = await stateManager.LoadOptimizationStateAsync(optimizationId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.Population);
        Assert.Equal(5, loaded.Population.CurrentGeneration);
        Assert.True(loaded.Population.Evaluated);
        Assert.Equal(0.95, loaded.Population.BestOverallFitness);
        Assert.Equal(2, loaded.Population.StagnationCount);
        Assert.True(loaded.Population.HyperMutation);
        Assert.Null(loaded.Population.NeuralNetworkState);
    }

    [Fact]
    public async Task SaveOptimizationState_WithNullPopulation_PersistsNull()
    {
        var stateManager = new StateManager();
        var optimizationId = $"opt_{Guid.NewGuid():N}";
        var state = new OptimizationState
        {
            TaskId = optimizationId,
            Config = new object(),
            Population = null,
            CurrentGeneration = 0,
            BestFitness = 0.0,
            StartTime = DateTime.UtcNow
        };

        await stateManager.SaveOptimizationStateAsync(optimizationId, state, CancellationToken.None);

        var loaded = await stateManager.LoadOptimizationStateAsync(optimizationId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Null(loaded!.Population);
    }
}
