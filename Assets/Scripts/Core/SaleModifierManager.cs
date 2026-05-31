using System.Collections.Generic;
using FactoryDelivery.Data;
using UnityEngine;

namespace FactoryDelivery.Core
{
    public class SaleModifierManager : MonoBehaviour
    {
        public SaleResult CalculateSale(SaleContext context)
        {
            ResourceDataSO resource = context.Resource;
            int amount = Mathf.Max(0, context.Amount);
            int baseUnitValue = resource != null ? Mathf.Max(0, resource.BaseValue) : 0;
            float unitMultiplier = 1f;
            int flatUnitBonus = 0;
            var notes = new List<string>();

            // TODO(Edict): [달빛 상단의 밀수품] 장착 여부를 확인하고,
            // context.IsNightSale == true일 때 unitMultiplier *= 2.5f를 적용한다.
            // 이 로직은 향후 EdictManager.HasEquipped(EdictType.MoonlightSmuggler) 같은 API가 생기면 연결한다.

            // TODO(Edict): [어전회의의 극단적 결단]은 ResourceDataSO.IsRawResource를 기준으로
            // 원자재는 unitMultiplier *= 0.5f, 가공품은 unitMultiplier *= 2f를 적용한다.

            // TODO(Edict): [보릿고개의 구휼]은 전체 인벤토리의 0개 보유 자원 종류 수를 SaleContext에 추가해
            // 자원 가치 상승분을 flatUnitBonus 또는 unitMultiplier로 반영한다.

            // TODO(Gift): [실학자의 서책]으로 특정 자원의 기본 시세가 상승하면
            // 런 단위 가격 보정 테이블을 여기에서 flatUnitBonus에 더한다.

            // TODO(Market): 날짜별/자원별 시세 변동이 생기면 baseUnitValue 이후, 어명 배율 이전에 반영한다.

            int finalUnitValue = Mathf.Max(0, Mathf.RoundToInt((baseUnitValue + flatUnitBonus) * unitMultiplier));
            int totalValue = finalUnitValue * amount;

            if (finalUnitValue != baseUnitValue)
            {
                notes.Add($"단가 {baseUnitValue} -> {finalUnitValue}");
            }

            return new SaleResult(baseUnitValue, finalUnitValue, totalValue, notes);
        }
    }
}
