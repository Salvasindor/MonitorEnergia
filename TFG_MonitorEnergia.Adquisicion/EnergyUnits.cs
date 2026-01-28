namespace TFG_MonitorEnergia.SessionData;

public enum EnergyUnit
{
    Wh,
    J
}

public static class EnergyConvert
{
    public static double JoulesToWh(double joules) => joules / 3600.0;
    public static double WhToJoules(double wh) => wh * 3600.0;
}