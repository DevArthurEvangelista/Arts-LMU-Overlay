using System;

namespace Arthur.LmuOverlay
{
    public static class CarIdentity
    {
        static readonly string[,] makes={
            {"aston martin","astonmartin"},{"astonmartin","astonmartin"},{"porsche","porsche"},
            {"ferrari","ferrari"},{"bmw","bmw"},{"mercedes","mercedes"},{"amg gt","mercedes"},
            {"mclaren","mclaren"},{"ford","ford"},{"corvette","chevrolet"},{"chevrolet","chevrolet"},
            {"toyota","toyota"},{"peugeot","peugeot"},{"lamborghini","lamborghini"},{"cadillac","cadillac"},
            {"alpine","alpine"},{"lexus","lexus"}
        };
        public static string Make(Driver driver)
        {
            foreach(string value in new[]{driver.CarName,driver.VehicleKey})
            {
                if(string.IsNullOrEmpty(value)) continue;
                string name=value.ToLowerInvariant().Replace('_',' ');
                for(int i=0;i<makes.GetLength(0);i++) if(name.Contains(makes[i,0])) return makes[i,1];
            }
            return "";
        }
    }
    public static class TyreCompound
    {
        public static string Code(string value)
        {
            value=(value ?? "").Trim().ToUpperInvariant();
            if(value.Contains("WET") || value.Contains("CHUVA")) return "W";
            if(value.Contains("SOFT") || value.Contains("MACIO")) return "S";
            if(value.Contains("MEDIUM") || value.Contains("MÉDIO") || value.Contains("MEDIO")) return "M";
            if(value.Contains("HARD") || value.Contains("DURO")) return "H";
            return "?";
        }
    }
}
