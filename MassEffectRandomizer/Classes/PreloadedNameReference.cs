namespace MassEffectRandomizer.Classes
{
    internal class PreloadedNameReference
    {
        private int nameIndex; //0
        private int nameValueIndex; //4

        public PreloadedNameReference(int v1, int v2)
        {
            this.nameIndex = v1;
            this.nameValueIndex = v2;
        }

        public int GetNameIndex()
        {
            return nameIndex;
        }

        public int GetNameValueIndex()
        {
            return nameValueIndex;
        }
    }
}