using LiteNetLib.Utils;
public static partial class SerializableBasis
{
    public struct LocalAvatarSyncMessage
    {
        public float X;
        public float Y;
        public float Z;

        public float XQ;
        public float YQ;
        public float ZQ;
        public float WQ;

        public float[] MuscleArray;

        public ushort XS;
        public ushort YS;
        public ushort ZS;
        public void Deserialize(NetDataReader Writer)
        {
            Writer.TryGetFloat(out X);
            Writer.TryGetFloat(out Y);
            Writer.TryGetFloat(out Z);

            Writer.TryGetFloat(out XQ);
            Writer.TryGetFloat(out YQ);
            Writer.TryGetFloat(out ZQ);
            Writer.TryGetFloat(out WQ);

            for (int Index = 0; Index < 90; Index++)
            {
                Writer.TryGetFloat(out MuscleArray[Index]);
            }
            if (Writer.EndOfData)
            {
                XS = 1;
                YS = 1;
                ZS = 1;
            }
            else
            {
                if (Writer.AvailableBytes >= 6)
                {
                    Writer.TryGetUShort(out XS);
                    Writer.TryGetUShort(out YS);
                    Writer.TryGetUShort(out ZS);
                }
                else
                {
                    Writer.TryGetUShort(out XS);
                    YS = XS;
                    ZS = XS;
                }
            }
        }
        public void Serialize(NetDataWriter Writer)
        {

            Writer.Put(X);
            Writer.Put(Y);
            Writer.Put(Z);

            Writer.Put(XQ);
            Writer.Put(YQ);
            Writer.Put(ZQ);
            Writer.Put(WQ);
            for (int Index = 0; Index < 90; Index++)
            {
                Writer.Put(MuscleArray[Index]);
            }
            /*
            if (XS == 1 && YS == 1 && ZS == 1)
            {
                // If all values are exactly 1, serialize only one value
                Writer.Put((ushort)1); // Use a single value to save space
            }
            else
            {
                // Check if XS, YS, and ZS are approximately equal
                if (Math.Abs(XS - YS) < EPSILON && Math.Abs(YS - ZS) < EPSILON)
                {
                    // If approximately equal, write a single value for optimization (optional)
                    Writer.Put(XS);
                }
                else
                {
                    // If not approximately equal, write all three values
                    Writer.Put(XS);
                    Writer.Put(YS);
                    Writer.Put(ZS);
                }
            }
            */
        }
        // const float EPSILON = 0.0001f; // Define a small value for approximate comparison
        public void Dispose()
        {
        }

    }
}