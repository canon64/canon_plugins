using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace MainGameAiShoujoMode.Conversation
{
    internal static class AiConversationItemOffers
    {
        private sealed class Mention { internal string Name; internal int Start,Length; }
        // Offer only objects named by the player. Do not turn the whole inventory into a prompt.
        internal static string[] MentionedNames(string text,IEnumerable<string> names)
        {
            if(string.IsNullOrWhiteSpace(text))return Array.Empty<string>();
            text=text.Normalize(NormalizationForm.FormKC);
            var found=new List<Mention>();
            foreach(var name in names.Where(n=>!string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal))
            {
                var normalized=name.Normalize(NormalizationForm.FormKC);var start=default(int);
                while(start<text.Length)
                {
                    var at=text.IndexOf(normalized,start,StringComparison.OrdinalIgnoreCase);if(at<default(int))break;
                    bool Word(char c)=>(c>='a'&&c<='z')||(c>='A'&&c<='Z')||char.IsDigit(c);
                    var end=at+normalized.Length;
                    if(!(Word(normalized.First())&&at>default(int)&&Word(text[at-Math.Sign(int.MaxValue)]))
                        &&!(Word(normalized.Last())&&end<text.Length&&Word(text[end])))found.Add(new Mention{Name=name,Start=at,Length=normalized.Length});
                    start=at+Math.Sign(int.MaxValue);
                }
            }
            return found.Where(m=>!found.Any(other=>other.Start<=m.Start&&other.Start+other.Length>=m.Start+m.Length&&other.Length>m.Length)).Select(m=>m.Name).Distinct(StringComparer.Ordinal).ToArray();
        }
        internal static AiReplyAction[] ForWire(IEnumerable<AiReplyAction> actions)=>actions.Select(a=>new AiReplyAction
        {Action=a.Action,Target=a.Target,Label=a.Action=="receive_item"||a.Action=="eat"||a.Target?.StartsWith("bed:",StringComparison.Ordinal)==true?a.Label:null}).ToArray();
    }
}
