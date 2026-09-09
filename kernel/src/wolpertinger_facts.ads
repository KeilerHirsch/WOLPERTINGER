with Interfaces;
with Wolpertinger_State;
with Wolpertinger_Types;

package Wolpertinger_Facts with SPARK_Mode is

   package State_Types renames Wolpertinger_State;
   package Types renames Wolpertinger_Types;

   type Jump_Fact is record
      Cursor              : Types.Observation_Cursor;
      System_Address      : Interfaces.Unsigned_64 := 0;
      Star_System         : Types.Text_128;
      Position            : Types.Galactic_Position;
      Jump_Distance       : Types.Decimal_64;
      Fuel_Used           : Types.Decimal_64;
      Fuel_Level          : Types.Decimal_64;
      Location_Provenance : State_Types.Provenance_Kind := Types.Unknown_Source;
      Location_Freshness  : State_Types.Freshness_State := State_Types.Unknown;
      Fuel_Provenance     : State_Types.Provenance_Kind := Types.Unknown_Source;
      Fuel_Freshness      : State_Types.Freshness_State := State_Types.Unknown;
   end record;

end Wolpertinger_Facts;
