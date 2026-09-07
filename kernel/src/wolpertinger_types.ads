with Interfaces;

package Wolpertinger_Types is
   pragma SPARK_Mode;

   subtype Exponent_10 is Integer range -18 .. 18;

   type Decimal_64 is record
      Coefficient : Interfaces.Integer_64 := 0;
      Exponent    : Exponent_10 := 0;
   end record;

   type Observation_Cursor is record
      Evidence_Sequence : Interfaces.Unsigned_64 := 0;
      Message_Ordinal   : Interfaces.Unsigned_32 := 0;
   end record;

   type Galaxy_Realm is (Unknown, Live, Legacy, Beta_Or_PTS);
   type Observation_Kind is (Session_Bound, FSD_Jump);
   type Source_Provenance is
     (Unknown_Source,
      Local_Journal,
      Local_Status,
      Frontier_API,
      Community,
      User_Entered);

   type Byte_16 is array (Positive range 1 .. 16) of Interfaces.Unsigned_8;
   type Byte_32 is array (Positive range 1 .. 32) of Interfaces.Unsigned_8;

   type Text_64 is record
      Length : Natural range 0 .. 64 := 0;
      Data   : String (1 .. 64) := [others => Character'Val (0)];
   end record;

   type Text_128 is record
      Length : Natural range 0 .. 128 := 0;
      Data   : String (1 .. 128) := [others => Character'Val (0)];
   end record;

   type Profile_Key is record
      FID        : Text_64;
      Realm      : Galaxy_Realm := Unknown;
      Save_Epoch : Interfaces.Unsigned_64 := 0;
   end record;

   type Galactic_Position is record
      X : Decimal_64;
      Y : Decimal_64;
      Z : Decimal_64;
   end record;

   type FSD_Jump_Data is record
      Star_System   : Text_128;
      System_Address : Interfaces.Unsigned_64 := 0;
      Position       : Galactic_Position;
      Jump_Distance  : Decimal_64;
      Fuel_Used      : Decimal_64;
      Fuel_Level     : Decimal_64;
   end record;

   type Observation is record
      Cursor              : Observation_Cursor;
      Evidence_Digest     : Byte_32 := [others => 0];
      Session_Id          : Byte_16 := [others => 0];
      Profile             : Profile_Key;
      Kind                : Observation_Kind := Session_Bound;
      Source_Time_Present : Boolean := False;
      Source_Time_Unix_Ms : Interfaces.Integer_64 := 0;
      Observed_Unix_Ms    : Interfaces.Integer_64 := 0;
      Commit_Unix_Ms      : Interfaces.Integer_64 := 0;
      Message_Count       : Interfaces.Unsigned_16 := 1;
      Provenance          : Source_Provenance := Unknown_Source;
      Jump                : FSD_Jump_Data;
   end record;

end Wolpertinger_Types;
