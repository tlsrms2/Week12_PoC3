namespace FactoryDelivery.Core
{
    /// <summary>
    /// 영구적인 패시브 효과를 제공하는 어명(Mandate)의 종류입니다.
    /// </summary>
    public enum MandateType
    {
        None,
        /// <summary>[연리지의 축복]: 가공 시설 처리 속도 영구 20% 증가</summary>
        BlessingOfCoupledTrees,
        /// <summary>[광폭 도로 포장]: 일꾼 이동 속도 영구 2배</summary>
        WideRoadPaving,
        /// <summary>[야행성 인부]: 낮에는 일꾼 속도 30% 감소, 밤에는 3배 폭증</summary>
        NocturnalWorkers,
        /// <summary>[백야의 노동]: 밤 시간 동안 일꾼 이동 속도 50% 증가, 가공 속도 2배</summary>
        WhiteNightLabor,
        /// <summary>[장인의 손길]: 새 가공 시설 배치 시 15% 확률로 레벨 2로 시작</summary>
        ArtisansTouch,
        /// <summary>[일필휘지의 도로]: 도로 건설 비용 50% 감소, 커브에서 속도 저하</summary>
        MasterStrokeRoad
    }

    /// <summary>
    /// 슬롯에 장착하여 수익 시너지를 내는 교지(Edict)의 종류입니다.
    /// </summary>
    public enum EdictType
    {
        None,
        /// <summary>[호조판서의 쌀가마니]: 쌀 200개 이상 보유 시 메주/무명천 판매가 2.5배</summary>
        RiceBagsOfHojo,
        /// <summary>[직진의 기개]: 창고 수납 직전 마지막 3칸이 직선이면 수량 2배 (TODO: 물류 시스템 로직 연동 필요)</summary>
        SpiritOfStraightPath,
        /// <summary>[청백리의 텅 빈 마당]: 맵 위 미건설 빈칸 15칸 이상 유지 시 판매가 보너스</summary>
        CleanOfficialsYard,
        /// <summary>[어전회의의 극단적 결단]: 원자재 반값, 가공품 수익 2배</summary>
        ExtremeDecision,
        /// <summary>[달빛 상단의 밀수품]: 밤 시간 자원 판매가 2.5배</summary>
        MoonlightSmuggler,
        /// <summary>[보릿고개의 구휼]: 보유량 0인 자원 수만큼 다른 자원 가치 상승</summary>
        SpringHungerRelief,
        /// <summary>[과적의 미학]: 특상품 판매 시 2배</summary>
        AestheticsOfOverload,
        /// <summary>[호조의 재고 정리]: 정산 직전 모든 1차 자원 1.5배 강제 판매</summary>
        HojoInventoryClearing
    }
}
