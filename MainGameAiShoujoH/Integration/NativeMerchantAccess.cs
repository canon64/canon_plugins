using System;
using System.Linq;
using MainGameAiShoujoH.Contracts;
using MainGameAiShoujoH.Progression;

namespace MainGameAiShoujoH.Integration
{
    public static class NativeMerchantAccess
    {
        public static int Limit(int cumulativeSpend) => cumulativeSpend <= 500 ? 0 : cumulativeSpend <= 2000 ? 1 : 2;
        public static void Validate(StartRequest request, string motionId)
        {
            if (request.VoiceContexts == null || !request.VoiceContexts[0].Merchant) return;
            int limit = Limit(request.ReadMerchantSpendTotal?.Invoke() ?? throw new InvalidOperationException("商人累積取引実績が未接続です。"));
            if (limit == 0) throw new InvalidOperationException("原本の商人H条件を満たす取引実績がありません（500以下）。");
            var definition = NativeProcCatalog.Get(motionId);
            if (request.SecondFemaleIsPlayer && !request.Settings.Futanari)
            { if (definition.Kind != ProcKind.Les) throw new InvalidOperationException("原本商人の女性主人公条件ではレズ体位を選択してください。"); }
            else if (definition.Kind == ProcKind.Les || limit == 1 && definition.Kind != ProcKind.Houshi
                || definition.Kind == ProcKind.MultiPlay && !request.Participants.Any(p => p.Role == "female2"))
                throw new InvalidOperationException("原本の商人取引段階・参加者条件でこの体位は選択できません。");
        }
    }
}
