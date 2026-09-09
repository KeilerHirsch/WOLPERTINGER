with AUnit.Assertions;
with Interfaces;
with Wolpertinger_Bounded_Text;
with Wolpertinger_Engine;
with Wolpertinger_State;
with Wolpertinger_Types;

procedure Test_Engine is
   package Assert renames AUnit.Assertions;
   package Text renames Wolpertinger_Bounded_Text;
   package Engine renames Wolpertinger_Engine;
   package State_Types renames Wolpertinger_State;
   package Types renames Wolpertinger_Types;

   use type Interfaces.Integer_64;
   use type Interfaces.Unsigned_64;
   use type Engine.Apply_Status;
   use type State_Types.Freshness_State;
   use type State_Types.Kernel_State;
   use type Types.Byte_16;
   use type Types.Decimal_64;
   use type Types.Observation_Cursor;
   use type Types.Profile_Key;
   use type Types.Source_Provenance;

   Session_Id : constant Types.Byte_16 := [others => 16#A0#];
   Profile : constant Types.Profile_Key :=
     (FID        => Text.To_Text_64 ("FTEST0001"),
      Realm      => Types.Live,
      Save_Epoch => 0);
   function Session_Observation return Types.Observation is
   begin
      return
        (Cursor              => (Evidence_Sequence => 1, Message_Ordinal => 0),
         Evidence_Digest     => [others => 16#11#],
         Session_Id          => Session_Id,
         Profile             => Profile,
         Kind                => Types.Session_Bound,
         Source_Time_Present => True,
         Source_Time_Unix_Ms => 1_700_000_000_000,
         Observed_Unix_Ms    => 1_700_000_000_100,
         Commit_Unix_Ms      => 1_700_000_000_200,
         Message_Count       => 1,
         Provenance          => Types.Local_Journal,
         Jump                => (others => <>));
   end Session_Observation;

   function Jump_Observation return Types.Observation is
   begin
      return
        (Cursor              => (Evidence_Sequence => 2, Message_Ordinal => 0),
         Evidence_Digest     => [others => 16#22#],
         Session_Id          => Session_Id,
         Profile             => Profile,
         Kind                => Types.FSD_Jump,
         Source_Time_Present => True,
         Source_Time_Unix_Ms => 1_700_000_001_000,
         Observed_Unix_Ms    => 1_700_000_001_100,
         Commit_Unix_Ms      => 1_700_000_001_200,
         Message_Count       => 1,
         Provenance          => Types.Local_Journal,
         Jump                =>
           (Star_System    => Text.To_Text_128 ("W. Grantler NX-42"),
            System_Address => 1_234_567_890_123_456_789,
            Position       =>
              (X => (Coefficient => 12_345, Exponent => -3),
               Y => (Coefficient => -6_789, Exponent => -2),
               Z => (Coefficient => 42, Exponent => 0)),
            Jump_Distance  => (Coefficient => 55_359, Exponent => -3),
            Fuel_Used      => (Coefficient => 4_843_642, Exponent => -6),
            Fuel_Level     => (Coefficient => 27_123, Exponent => -3)));
   end Jump_Observation;

   procedure Test_Happy_Path is
      State : State_Types.Kernel_State;
      Result : Engine.Apply_Result;
      Session : constant Types.Observation := Session_Observation;
      Jump : constant Types.Observation := Jump_Observation;
   begin
      Engine.Apply (State, Session, Result);
      Assert.Assert (Result.Status = Engine.Applied, "session must bind");
      Assert.Assert (State.Bound, "state must be bound");
      Assert.Assert (State.Profile = Profile, "profile must match");
      Assert.Assert (State.Session_Id = Session_Id, "session id must match");
      Engine.Apply (State, Jump, Result);
      Assert.Assert (Result.Status = Engine.Applied, "jump must apply");
      Assert.Assert
        (State.Location.System_Address = Jump.Jump.System_Address,
         "jump must update location");
      Assert.Assert
        (State.Location.Provenance = Types.Local_Journal,
         "location provenance must remain explicit");
      Assert.Assert
        (State.Location.Freshness = State_Types.Current,
         "fresh journal location must be current");
      Assert.Assert
        (State.Fuel.Level = Jump.Jump.Fuel_Level,
         "fuel level must update");
      Assert.Assert
        (State.Last_Cursor = Jump.Cursor,
         "last cursor must advance to jump");
      Assert.Assert (Result.Has_Jump_Fact, "applied jump must emit a fact");
      Assert.Assert
        (Result.Jump.System_Address = State.Location.System_Address,
         "jump fact must reflect authoritative location");
      Assert.Assert
        (Result.Jump.Location_Freshness = State.Location.Freshness,
         "fact freshness must come from authoritative state");
   end Test_Happy_Path;

   procedure Test_Ordering_And_Identity is
      State : State_Types.Kernel_State;
      Before : State_Types.Kernel_State;
      Result : Engine.Apply_Result;
      Session : Types.Observation := Session_Observation;
      Jump : Types.Observation := Jump_Observation;
   begin
      Jump.Cursor := (Evidence_Sequence => 1, Message_Ordinal => 0);
      Before := State;
      Engine.Apply (State, Jump, Result);
      Assert.Assert
        (Result.Status = Engine.Identity_Conflict,
         "jump before binding must fail identity");
      Assert.Assert (State = Before, "identity rejection must not mutate state");

      Engine.Apply (State, Session, Result);
      Assert.Assert (Result.Status = Engine.Applied, "session must bind once");
      Before := State;
      Engine.Apply (State, Session, Result);
      Assert.Assert (Result.Status = Engine.Idempotent, "same cursor/digest must be idempotent");
      Assert.Assert (State = Before, "idempotent resend must not mutate state");

      Session.Evidence_Digest (1) := 16#99#;
      Engine.Apply (State, Session, Result);
      Assert.Assert
        (Result.Status = Engine.Integrity_Fault,
         "same cursor/different digest must fault");
      Assert.Assert (State = Before, "integrity fault must not mutate state");

      Jump := Jump_Observation;
      Jump.Cursor := (Evidence_Sequence => 3, Message_Ordinal => 0);
      Engine.Apply (State, Jump, Result);
      Assert.Assert (Result.Status = Engine.Sequence_Gap, "future cursor must gap");
      Assert.Assert (State = Before, "sequence gap must not mutate state");
      Jump := Jump_Observation;
      Jump.Profile.FID := Text.To_Text_64 ("FOTHER999");
      Engine.Apply (State, Jump, Result);
      Assert.Assert
        (Result.Status = Engine.Identity_Conflict,
         "profile mismatch must fail closed");
      Assert.Assert (State = Before, "identity conflict must not mutate state");
   end Test_Ordering_And_Identity;

begin
   Test_Happy_Path;
   Test_Ordering_And_Identity;
end Test_Engine;
