    public class UsageServer : MySessionComponentBase
    {
        public override void LoadData()
        {
            base.LoadData();
            ApiServer.Load();
        }
        
        protected override void UnloadData()
        {
            base.UnloadData();
            ApiServer.Unload();
        }

        [DefaultValue("API:InvokeCommand")]
        public static CommandContext InvokeCommand(int command)
        {
            return new CommandContext(command);
        }

        public class CommandContext
        {
            private readonly int _command;

            public CommandContext(int command)
            {
                this._command = command;
            }

            [DefaultValue("API:CommandContextGetId:GetId")]
            public int GetId()
            {
                return _command;
            }
        }
    }

    public class UsageClient : MySessionComponentBase
    {
        public override void LoadData()
        {
            base.LoadData();
            ApiClient.Load();
        }

        protected override void UnloadData()
        {
            base.UnloadData();
            ApiClient.Unload();
        }

        public override void UpdateAfterSimulation()
        {
            if (ApiClient.IsReady)
            {
                var command = ApiClient.UsageServer.InvokeCommand(11);
                var id = command.GetId();
            }
        }
    }