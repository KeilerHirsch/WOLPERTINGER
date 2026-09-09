package body Wolpertinger_Control with SPARK_Mode is
   procedure Set_Role
     (State     : in out Control_State;
      New_Epoch : Interfaces.Unsigned_64;
      New_Role  : Kernel_Role;
      Accepted  : out Boolean) is
   begin
      if New_Epoch <= State.Current_Epoch then
         Accepted := False;
         return;
      end if;

      State.Current_Epoch := New_Epoch;
      State.Role := New_Role;
      Accepted := True;
   end Set_Role;
end Wolpertinger_Control;
