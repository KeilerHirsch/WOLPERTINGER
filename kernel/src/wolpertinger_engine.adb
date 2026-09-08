package body Wolpertinger_Engine with SPARK_Mode is

   use type Types.Galaxy_Realm;

   function Make_Jump_Fact
     (State : State_Types.Kernel_State) return Facts.Jump_Fact is
   begin
      return
        (Cursor              => State.Last_Cursor,
         System_Address      => State.Location.System_Address,
         Star_System         => State.Location.Star_System,
         Position            => State.Location.Position,
         Jump_Distance       => State.Last_Jump.Jump_Distance,
         Fuel_Used           => State.Fuel.Used,
         Fuel_Level          => State.Fuel.Level,
         Location_Provenance => State.Location.Provenance,
         Location_Freshness  => State.Location.Freshness,
         Fuel_Provenance     => State.Fuel.Provenance,
         Fuel_Freshness      => State.Fuel.Freshness);
   end Make_Jump_Fact;

   procedure Apply
     (State       : in out State_Types.Kernel_State;
      Observation : Types.Observation;
      Result      : out Apply_Result)
   is
      Candidate : State_Types.Kernel_State := State;
   begin
      Result := (others => <>);
      if State.Has_Last_Cursor
        and then State_Types.Same_Cursor (State.Last_Cursor, Observation.Cursor)
      then
         Result.Status :=
           (if State_Types.Same_Digest (State.Last_Digest, Observation.Evidence_Digest)
            then Idempotent
            else Integrity_Fault);
         return;
      end if;

      if not State_Types.Is_Next_Cursor (State, Observation) then
         Result.Status := Sequence_Gap;
         return;
      end if;

      case Observation.Kind is
         when Types.Session_Bound =>
            if State.Bound
              or else Observation.Profile.FID.Length = 0
              or else Observation.Profile.Realm = Types.Unknown
            then
               Result.Status := Identity_Conflict;
               return;
            end if;

            Candidate.Bound := True;
            Candidate.Profile := Observation.Profile;
            Candidate.Session_Id := Observation.Session_Id;

         when Types.FSD_Jump =>
            if not State_Types.Identity_Matches (State, Observation) then
               Result.Status := Identity_Conflict;
               return;
            end if;
            Candidate.Location.Known := True;
            Candidate.Location.System_Address := Observation.Jump.System_Address;
            Candidate.Location.Star_System := Observation.Jump.Star_System;
            Candidate.Location.Position := Observation.Jump.Position;
            Candidate.Location.Provenance := Observation.Provenance;
            Candidate.Location.Freshness := State_Types.Current;

            Candidate.Fuel.Known := True;
            Candidate.Fuel.Level := Observation.Jump.Fuel_Level;
            Candidate.Fuel.Used := Observation.Jump.Fuel_Used;
            Candidate.Fuel.Provenance := Observation.Provenance;
            Candidate.Fuel.Freshness := State_Types.Current;
            Candidate.Last_Jump := Observation.Jump;
      end case;

      Candidate.Has_Last_Cursor := True;
      Candidate.Last_Cursor := Observation.Cursor;
      Candidate.Last_Digest := Observation.Evidence_Digest;
      Candidate.Last_Message_Count := Observation.Message_Count;

      State := Candidate;
      Result.Status := Applied;
      if Observation.Kind = Types.FSD_Jump then
         Result.Has_Jump_Fact := True;
         Result.Jump := Make_Jump_Fact (State);
      end if;
   end Apply;

end Wolpertinger_Engine;
