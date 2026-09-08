with Interfaces;
with Wolpertinger_Types;

package Wolpertinger_State with SPARK_Mode is

   package Types renames Wolpertinger_Types;

   use type Types.Byte_16;

   subtype Provenance_Kind is Types.Source_Provenance;
   type Freshness_State is (Unknown, Current, Stale, Conflicting);

   type Location_State is record
      Known          : Boolean := False;
      System_Address : Interfaces.Unsigned_64 := 0;
      Star_System    : Types.Text_128;
      Position       : Types.Galactic_Position;
      Provenance     : Provenance_Kind := Types.Unknown_Source;
      Freshness      : Freshness_State := Unknown;
   end record;

   type Fuel_State is record
      Known      : Boolean := False;
      Level      : Types.Decimal_64;
      Used       : Types.Decimal_64;
      Provenance : Provenance_Kind := Types.Unknown_Source;
      Freshness  : Freshness_State := Unknown;
   end record;
   type Kernel_State is record
      Bound              : Boolean := False;
      Profile            : Types.Profile_Key;
      Session_Id         : Types.Byte_16 := [others => 0];
      Has_Last_Cursor    : Boolean := False;
      Last_Cursor        : Types.Observation_Cursor;
      Last_Digest        : Types.Byte_32 := [others => 0];
      Last_Message_Count : Interfaces.Unsigned_16 := 1;
      Location           : Location_State;
      Fuel               : Fuel_State;
      Last_Jump          : Types.FSD_Jump_Data;
   end record;

   function Same_Profile
     (Left, Right : Types.Profile_Key) return Boolean;

   function Same_Cursor
     (Left, Right : Types.Observation_Cursor) return Boolean;

   function Same_Digest
     (Left, Right : Types.Byte_32) return Boolean;

   function Cursor_Is_Valid
     (Observation : Types.Observation) return Boolean;

   function Is_Next_Cursor
     (State : Kernel_State;
      Observation : Types.Observation) return Boolean;

function Identity_Matches
     (State : Kernel_State;
      Observation : Types.Observation) return Boolean
     with Post =>
       (Identity_Matches'Result =
          (State.Bound
           and then State.Session_Id = Observation.Session_Id
           and then Same_Profile (State.Profile, Observation.Profile)));

end Wolpertinger_State;
