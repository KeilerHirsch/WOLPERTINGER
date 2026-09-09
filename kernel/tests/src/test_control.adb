with AUnit.Assertions;
with Interfaces;
with Wolpertinger_Control;

procedure Test_Control is
   package Assert renames AUnit.Assertions;
   package Control renames Wolpertinger_Control;

   use type Interfaces.Unsigned_64;
   use type Control.Kernel_Role;

   State    : Control.Control_State;
   Accepted : Boolean;
begin
   Assert.Assert (State.Current_Epoch = 0, "kernel starts at epoch zero");
   Assert.Assert (State.Role = Control.Shadow, "kernel starts shadow");

   Control.Set_Role (State, 1, Control.Active, Accepted);
   Assert.Assert (Accepted, "epoch one promotion accepted");
   Assert.Assert (State.Current_Epoch = 1, "epoch must advance to one");
   Assert.Assert (State.Role = Control.Active, "role must become active");

   Control.Set_Role (State, 1, Control.Shadow, Accepted);
   Assert.Assert (not Accepted, "equal epoch must be rejected");
   Assert.Assert (State.Current_Epoch = 1 and State.Role = Control.Active,
                  "equal epoch rejection must not mutate control state");

   Control.Set_Role (State, 0, Control.Shadow, Accepted);
   Assert.Assert (not Accepted, "stale epoch must be rejected");
   Assert.Assert (State.Current_Epoch = 1 and State.Role = Control.Active,
                  "stale epoch rejection must not mutate control state");

   Control.Set_Role (State, 2, Control.Shadow, Accepted);
   Assert.Assert (Accepted, "newer epoch demotion accepted");
   Assert.Assert (State.Current_Epoch = 2, "epoch must advance to two");
   Assert.Assert (State.Role = Control.Shadow, "role must become shadow");
end Test_Control;
