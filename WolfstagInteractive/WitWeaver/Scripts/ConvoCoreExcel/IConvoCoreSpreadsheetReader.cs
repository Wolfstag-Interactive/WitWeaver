using System.Collections.Generic;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// Defines the contract for reading a spreadsheet file into ConvoCore's internal
    /// dialogue configuration format.
    ///
    /// Sheet name = ConversationKey.
    /// Header row columns must include the CharacterID and LineID column names as configured
    /// in ConvoCoreSettings. At least one language code column is required per sheet.
    /// Unrecognized columns are preserved during writeback but ignored during import.
    /// Sheets whose names begin with the configured skip prefix are ignored.
    /// </summary>
    public interface IConvoCoreSpreadsheetReader
    {
        /// <summary>
        /// Reads the spreadsheet at the given absolute file path and returns a dictionary
        /// mapping conversation key to an ordered list of row configs (each pairing the
        /// 1-based xlsx row number with its dialogue configuration).
        /// Returns false and populates error on failure. Never throws.
        /// </summary>
        bool TryRead(
            string absolutePath,
            ConvoCoreSettings settings,
            out Dictionary<string, List<SpreadsheetRowConfig>> result,
            out string error);
    }
}
