using Xunit;
using ClearSkies.Domain;

namespace ClearSkies.Tests;

public class AviationCalculationsTests
{
    [Fact]
    public void Category_VFR_WhenGoodCeilingAndVis()
    {
        var cat = AviationCalculations.ComputeCategory(5000, 10m);
        Assert.Equal(FlightCategory.VFR, cat);
    }

    [Fact]
    public void Category_MVFR_WhenCeiling2500()
    {
        var cat = AviationCalculations.ComputeCategory(2500, 10m);
        Assert.Equal(FlightCategory.MVFR, cat);
    }

    [Fact]
    public void Category_IFR_WhenVis2SM()
    {
        var cat = AviationCalculations.ComputeCategory(6000, 2m);
        Assert.Equal(FlightCategory.IFR, cat);
    }

    [Fact]
    public void Category_LIFR_WhenCeiling400()
    {
        var cat = AviationCalculations.ComputeCategory(400, 10m);
        Assert.Equal(FlightCategory.LIFR, cat);
    }

    [Fact]
    public void Crosswind_90Runway_WindFromSouth10kt_Is10ktCrosswind()
    {
        var (head, cross) = AviationCalculations.WindComponents(90, 180, 10);
        Assert.Equal(0m, head);
        Assert.Equal(10m, cross);
    }

    [Fact]
    public void DensityAltitude_IncreasesWithHeat()
    {
        var cool = AviationCalculations.DensityAltitudeFt(500, 15m, 29.92m);
        var hot = AviationCalculations.DensityAltitudeFt(500, 35m, 29.92m);
        Assert.True(hot > cool);
    }
}
