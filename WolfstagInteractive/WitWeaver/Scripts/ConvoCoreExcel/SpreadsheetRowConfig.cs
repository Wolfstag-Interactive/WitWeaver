using UnityEngine;

namespace WolfstagInteractive.ConvoCore
{
    /// <summary>
    /// A parsed dialogue line paired with its exact source row number in the spreadsheet.
    /// The <see cref="XlRowNumber"/> is set by the parser and consumed by the writer so that
    /// LineID writeback targets the correct cell directly, with no index re-derivation.
    /// </summary>
[HelpURL("https://docs.wolfstaginteractive.com/convocore/api/classWolfstagInteractive_1_1ConvoCore_1_1SpreadsheetRowConfig.html")]
    public sealed class SpreadsheetRowConfig
    {
        /// <summary>
        /// 1-based row number as it appears in the .xlsx file.
        /// Used by <c>ConvoCoreExcelWriter</c> to locate the exact row for LineID writeback.
        /// </summary>
        public int XlRowNumber { get; }

        /// <summary>The parsed dialogue configuration for this row.</summary>
        public DialogueYamlConfig Config { get; }

        public SpreadsheetRowConfig(int xlRowNumber, DialogueYamlConfig config)
        {
            XlRowNumber = xlRowNumber;
            Config      = config;
        }
    }
}