with Wolpertinger_Facts;
with Wolpertinger_State;
with Wolpertinger_Types;

package Wolpertinger_Engine with SPARK_Mode is

   package Facts renames Wolpertinger_Facts;
   package State_Types renames Wolpertinger_State;
   package Types renames Wolpertinger_Types;

   use type State_Types.Freshness_State;
   use type State_Types.Kernel_State;
   use type Types.Observation_Cursor;
   use type Types.Observation_Kind;
   use type Types.Source_Provenance;

   type Apply_Status is
     (Applied,
      Idempotent,
      Sequence_Gap,
      Integrity_Fault,
      Identity_Conflict);

   type Apply_Result is record
      Status        : Apply_Status := Sequence_Gap;
      Has_Jump_Fact : Boolean := False;
      Jump          : Facts.Jump_Fact;
   end record;
   procedure Apply
     (State       : in out State_Types.Kernel_State;
      Observation : Types.Observation;
      Result      : out Apply_Result)
     with Post =>
       (Result.Status = Applied or else State = State'Old)
       and then
       (Result.Status /= Applied
        or else (State.Has_Last_Cursor
                 and then State.Last_Cursor = Observation.Cursor))
       and then
       (Result.Status /= Applied
        or else Observation.Kind /= Types.FSD_Jump
        or else
          (State_Types.Identity_Matches (State, Observation)
           and then State.Location.Provenance = Observation.Provenance
           and then State.Location.Freshness = State_Types.Current
           and then State.Fuel.Provenance = Observation.Provenance
           and then State.Fuel.Freshness = State_Types.Current))
       and then
       (Result.Has_Jump_Fact =
          (Result.Status = Applied
           and then Observation.Kind = Types.FSD_Jump));

end Wolpertinger_Engine;
