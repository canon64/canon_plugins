using System;
using System.Collections.Generic;
using System.Linq;

namespace MainGameAiShoujoMode.Conversation
{
    // Use the installed KKS data constructors for native optional cells and literal expressions.
    // These methods only construct managed records; they never create a scene or play media.
    internal sealed class KksAdvMediaArguments
    {
        private readonly int _zero, _one;
        internal KksAdvMediaArguments(int zero, int one) { _zero = zero; _one = one; }
        internal IReadOnlyList<ADV.Commands.Base.Voice.Data> Voices(KksAdvScript.Row row, Func<string, string> replace)
        {
            var args = Arguments(row, new ADV.Commands.Base.Voice().ArgsDefault, replace);
            var result = new List<ADV.Commands.Base.Voice.Data>();
            if (args.Length > _one)
            {
                var index = _zero;
                while (Has(args, index)) result.Add(new ADV.Commands.Base.Voice.Data(args, ref index));
            }
            return result.AsReadOnly();
        }
        internal IReadOnlyList<ADV.Commands.Base.Motion.Data> Motions(KksAdvScript.Row row, Func<string, string> replace, Func<string, string[]> findAlias)
        {
            var prototype = new ADV.Commands.Base.Motion();
            var args = Arguments(row, prototype.ArgsDefault, replace);
            var result = new List<ADV.Commands.Base.Motion.Data>();
            if (args.Length > _one)
            {
                var index = _zero;
                while (Has(args, index))
                {
                    var check = Cell(args, index + _one);
                    var alias = check == null ? null : findAlias?.Invoke(check);
                    if (alias != null)
                    {
                        var expanded = Enumerable.Repeat(string.Empty, prototype.ArgsLabel.Length - _one).ToArray();
                        for (var cell = _zero; cell < expanded.Length && cell < alias.Length; cell++)
                            if (alias[cell] != null) expanded[cell] = alias[cell];
                        args = args.Take(index + _one).Concat(expanded).Concat(args.Skip(index + _one + _one)).ToArray();
                    }
                    result.Add(new ADV.Commands.Base.Motion.Data(args, ref index));
                }
            }
            return result.AsReadOnly();
        }
        internal IReadOnlyList<ADV.Commands.Base.Expression.Data> Expressions(KksAdvScript.Row row, Func<string, string> replace,
            Func<int, string, Manager.Game.Expression> findNamed)
        {
            var args = Arguments(row, new ADV.Commands.Base.Expression().ArgsDefault, replace);
            var result = new List<ADV.Commands.Base.Expression.Data>();
            if (args.Length > _one)
            {
                var index = _zero;
                while (Has(args, index))
                {
                    var check = Cell(args, index + _one);
                    var named = check == null ? null : findNamed?.Invoke(int.Parse(args[index]), check);
                    if (named != null)
                    {
                        result.Add(new ADV.Commands.Base.Expression.Data(int.Parse(args[index]), named));
                        index += _one + _one;
                    }
                    else
                    {
                        var literal = new ADV.Commands.Base.Expression.Data(args, ref index) { isChangeSkip = true };
                        result.Add(literal);
                    }
                }
            }
            return result.AsReadOnly();
        }
        private string[] Arguments(KksAdvScript.Row row, string[] defaults, Func<string, string> replace)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (replace == null) throw new ArgumentNullException(nameof(replace));
            return row.InitializeArguments(defaults).Select(replace).ToArray();
        }
        private bool Has(string[] args, int index) => !string.IsNullOrEmpty(Cell(args, index));
        private string Cell(string[] args, int index) => index >= _zero && index < args.Length ? args[index] : null;
    }
}
