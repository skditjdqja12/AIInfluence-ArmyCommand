# AI Influence - Army Command

An addon for the Mount & Blade II: Bannerlord mod **AI Influence** that lets lords you talk to **raise and lead an army** for a goal you give them. The goal stays locked until it's achieved.

> Ask a king: *"Gather the lords and take Zerk 'Allz."* He raises an army, marches, and keeps besieging it until it falls. After that the army acts on its own again.

No external bridge or proxy is needed. The addon hooks into AI Influence directly and works with any AI backend configured in AI Influence.

## Features
- **New NPC action `create_army`**: the NPC raises an army from its kingdom's lords and leads it to a settlement.
  - Types: `siege` (enemy town/castle), `defend` (own settlement), `patrol` (any settlement).
  - Lords named in the dialogue are summoned. If none are named, the nearest lords are summoned automatically (default up to 6).
- **Locked objective**: while locked, the army leader doesn't retarget, cohesion stays above a minimum, the army gets grain if food runs out, and vanilla "soft" disbands (cohesion, food, inactivity, too few parties) are blocked.
  - When the siege target is captured, or a defend/patrol mission runs its days (default 5), the lock is released and the army acts on its own.
  - Give a new goal in dialogue and the same army is re-locked to it.
- **New NPC action `release_army`**: ends the lock; with `disband:true` the army is also dissolved.
- **Auto cancel**: the goal is cancelled if the leader dies or is captured, the army is destroyed, or the war with the target ends.
- **Leader with no party**: a lord resting in a settlement with no party (e.g. after being defeated) gathers a new party first.
- **Save-safe**: lock data is stored outside the save file, so removing the addon doesn't break saves. Release locked armies first (see Notes).
- **Languages**: English and Korean messages.

## Requirements
- Bannerlord v1.3.x (tested on v1.3.15)
- [Harmony](https://www.nexusmods.com/mountandblade2bannerlord/mods/2006) (Bannerlord.Harmony)
- **AI Influence v6.0.2** (the hooked methods are checked at startup; on an unsupported version the addon disables itself and shows a message)

## Installation
1. Download `AIInfluenceArmyCommand-x.y.z.zip` from the [Releases](../../releases) page.
2. Extract it into `...\Mount & Blade II Bannerlord\Modules\` so you get `Modules\AIInfluenceArmyCommand\SubModule.xml`.
3. In the launcher, enable **AI Influence - Army Command** and load it **after AI Influence**.

## How to use
Talk to a lord who belongs to a kingdom and ask plainly, using exact settlement names:
- "Raise an army and besiege *Zerk 'Allz*."
- "Summon *Lord A* and *Lord B* and defend *Karak Eksfilaz* for 7 days."
- "Call off the campaign." / "Disband the army."

The NPC decides whether to agree. If it agrees, it adds the action to its reply and the army is created after the conversation closes.

## Settings
`Documents\Mount and Blade II Bannerlord\Configs\AIInfluenceArmyCommand\settings.txt` is created on first launch:

| Key | Default | Meaning |
|---|---|---|
| AutoCallMax | 6 | Lords summoned automatically when none are named |
| AutoCallRadius | 150 | Search radius for automatic summoning |
| MinCohesion | 60 | Minimum cohesion while an objective is locked |
| LockObjectives | true | `false` = create armies without locking the goal |
| KeepFed | true | Give grain to locked armies that run out of food |
| DefaultMissionDays | 5 | Length of defend/patrol goals |
| PendingTimeoutHours | 24 | Cancel commands that can't start within this many in-game hours |

The log is `armycommand.log` in the same folder.

## Notes / limitations
- The action is advertised to NPCs through AI Influence's `siege_settlement` action prompt, so it's available to the same NPCs that can receive siege orders.
- Influence is not spent when an army is created.
- Lords currently doing a player-given AI Influence task may be excluded from the army by AI Influence itself.
- Before removing the addon, release locked armies in dialogue (`release_army`). Otherwise the leader may keep its "no new decisions" flag.
- If a bridge/proxy strips `create_army` from replies before the game sees them, this addon won't receive the command.

## Building from source
Requires the .NET SDK (any recent version; targets .NET Framework 4.7.2).
```powershell
.\build.ps1 -GameDir "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord"
```
Output: `dist\AIInfluenceArmyCommand\` and `dist\AIInfluenceArmyCommand-<version>.zip` (`-Install` copies it into the game's Modules folder).

## Credits
AI Influence is developed by its original author. This addon contains no AI Influence code. It references AI Influence's public methods at runtime through Harmony.

---

# AI Influence - Army Command (한국어)

**AI Influence** 모드와 대화하는 영주에게 목표를 주면, 그 영주가 **군대를 창설해 직접 이끄는** 애드온입니다. 목표는 달성될 때까지 고정됩니다. 브릿지나 프록시 없이 AI Influence에 직접 연결되며, AI Influence에 설정한 어떤 AI 백엔드에서도 동작합니다.

## 기능
- **군대 창설 (`create_army`)**: NPC가 왕국 영주들로 군대를 만들어 목표 거점으로 이끕니다.
  - 유형은 `siege`(적 도시·성 포위), `defend`(아군 거점 방어), `patrol`(순찰)입니다.
  - 대사에서 지목한 영주를 소집하고, 지목이 없으면 가까운 영주를 자동 소집합니다(기본 최대 6명).
- **목표 고정**: 고정 중에는 대장이 목표를 바꾸지 않습니다. 결속력은 최소치 아래로 떨어지지 않고, 식량이 떨어지면 곡물을 보충합니다. 결속력·식량·활동 부족 등으로 게임이 군대를 해산하려 하면 막습니다.
- **목표 달성 후 자율 행동**: 포위 목표를 점령하거나 방어·순찰 기간(기본 5일)이 끝나면 고정이 풀리고, 이후에는 AI가 자율적으로 움직입니다.
- **새 목표로 재고정**: 대화로 새 목표를 주면 같은 군대가 다시 고정됩니다.
- **고정 해제 (`release_army`)**: 고정을 풉니다. `disband:true`를 붙이면 군대도 해산합니다.
- **자동 취소**: 대장이 죽거나 포로가 되면, 군대가 괴멸되면, 목표와의 전쟁이 끝나면 목표가 자동으로 취소됩니다.
- **부대 없는 영주**: 부대 없이 거점에 머무는 영주는 먼저 부대를 새로 꾸립니다.
- **세이브 안전**: 고정 정보는 세이브 파일 밖에 저장돼서, 애드온을 제거해도 세이브가 깨지지 않습니다.

## 필요 조건
- **배너로드**: v1.3.x (v1.3.15에서 테스트)
- **Harmony**: 필요
- **AI Influence v6.0.2**: 다른 버전이면 게임 시작 시 확인해서, 스스로 기능을 끄고 메시지를 띄웁니다.

## 설치
1. [Releases](../../releases)에서 zip을 받습니다.
2. `Modules` 폴더에 압축을 풉니다.
3. 런처에서 **AI Influence 다음 순서**로 켭니다.

## 사용법
왕국 소속 영주에게 정확한 거점 이름으로 부탁합니다.
- 예: "군대를 모아 제르크 알즈를 포위하시오"
- 예: "A 영주와 B 영주를 소집해 7일간 카락 엑스필라즈를 방어하시오"
- 예: "원정을 중단하시오" / "군대를 해산하시오"

수락 여부는 NPC가 판단하고, 수락하면 대화창이 닫힌 뒤 군대가 만들어집니다.

## 설정
설정 파일(`settings.txt`)과 로그 파일(`armycommand.log`)은 `문서\Mount and Blade II Bannerlord\Configs\AIInfluenceArmyCommand\`에 있습니다. 각 항목의 의미는 위 영어 표를 참고하세요.

## 주의
- **표시되는 NPC**: 이 행동은 AI Influence의 포위 명령 프롬프트에 붙어서, 포위 명령을 받을 수 있는 NPC에게만 보입니다.
- **영향력**: 군대 창설에 영향력을 소모하지 않습니다.
- **소집 제외**: AI Influence의 플레이어 임무 중인 영주는 소집에서 빠질 수 있습니다.
- **제거 전**: 애드온을 제거하기 전에 고정 중인 군대를 대화로 해제하세요.
- **브릿지·프록시 사용 시**: 응답에서 `create_army`를 먼저 지우는 브릿지나 프록시를 쓰면, 이 애드온이 명령을 받지 못합니다.
