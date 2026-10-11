namespace RFramework
{
    /// <summary>框架保留的存档槽位名称。</summary>
    public static class StorageSlotNames
    {
        /// <summary>自动存档槽位。只能通过 SaveAutoAsync 写入。</summary>
        public const string AutoSave = "auto";

        internal static bool IsReserved(string slotName)
        {
            return string.Equals(slotName, AutoSave, System.StringComparison.Ordinal);
        }
    }
}
