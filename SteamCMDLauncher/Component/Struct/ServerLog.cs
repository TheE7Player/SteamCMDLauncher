namespace SteamCMDLauncher.Component.Struct
{
    public struct ServerLog
    {
        public int[] types;
        public string[] detail;
        public string[] utc_time;
        public bool Empty;

        public int Capacity => counter;
        
        private int counter;


        public ServerLog(int size = 10)
        {
            bool toInitialize = size > 0;

            counter = toInitialize ? 0 : -1;
            types = toInitialize ? new int[size] : null;
            detail = toInitialize ? new string[size] : null;
            utc_time = toInitialize ? new string[size] : null;

            Empty = true;
        }

        public void ResizeNew(int size)
        {
            if (!Empty) Destory();

            types = new int[size];
            detail = new string[size];
            utc_time = new string[size];
            counter = 0;
        }

        public void Add(int type, string detail, string utc)
        {
            if (counter == -1) return;

            this.types[counter] = type;
            this.detail[counter] = detail;
            this.utc_time[counter] = utc;
            
            counter++;
            Empty = false;
            detail = null;
            utc = null;
        }

        public void Destory()
        {
            types = null;
            detail = null;
            utc_time = null;
        }
    }
}
