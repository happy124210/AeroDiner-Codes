# 🍽 Customer System

![CustomerAI](https://github.com/user-attachments/assets/2d2fc1e8-86af-4230-a8dd-10b1339b1fb3)


레스토랑 시뮬레이션에서 손님(Customer)의 생성부터 이동, 대기, 주문, 식사, 결제, 퇴장까지의 **전체 흐름**을 관리하는 시스템입니다.  
**MVC(Model-View-Controller) + State Machine + Object Pooling**을 기반으로 설계되어 성능과 유지보수성을 모두 고려했습니다.

---

## 📂 폴더 구조

```
Customer/
 ├── Model
 │    ├── Customer.cs
 │    ├── CustomerData.cs
 │    └── CustomerRuntimeData.cs
 │
 ├── View
 │    └── CustomerView.cs
 │
 ├── Controller
 │    ├── CustomerController.cs
 │    └── CustomerState.cs
 │
 ├── Manager
 │    ├── CustomerManager.cs
 │    ├── CustomerSpawner.cs
 │    ├── TableManager.cs
 │    └── PoolManager.cs
```

---

## 1️⃣ 설계 개요

### 🔹 MVC 구조

| 레이어      | 담당 역할 |
|------------|---------------------------------------------|
| **Model**  | 순수 데이터 관리 (인내심, 주문, 테이블 정보) |
| **View**   | 시각적 표현 담당 (UI, 애니메이션, 이펙트)     |
| **Controller** | Model과 View 연결, 상태 제어, 외부 매니저와 상호작용 |

- **장점**
  - 게임 로직과 시각화 완전 분리 → 유지보수 용이
  - Model은 순수 C#
  - View 교체/수정 시 로직 영향 최소화

---

### 🔹 상태 머신 (State Machine)

- **핵심 파일**: `CustomerState.cs`
- **주요 상태 흐름**
  ```
  MovingToEntrance → MovingToSeat / MovingToLine → Ordering → Eating → Paying → Leaving
  ```
- **특징**
  - 상태별 로직을 독립 클래스로 분리
  - 조건 분기 최소화 → 가독성 & 유지보수성 향상
  - `AngryLeavingState` 같은 특수 상태도 손쉽게 추가 가능
 
    ![CustomerLeavingState](https://github.com/user-attachments/assets/ed04ea06-ff57-441a-89a1-a33c85be7f50)

---

### 🔹 객체 풀링 (Object Pooling)

- **핵심 파일**: `PoolManager.cs`
- 손님 생성/회수 시 `Instantiate` & `Destroy` 대신 풀 재사용
- **IPoolable 인터페이스**를 통해
  - `OnGetFromPool()` : 풀에서 꺼낼 때 초기화
  - `OnReturnToPool()` : 풀로 반환 시 정리
 
    ![ObjectPooling](https://github.com/user-attachments/assets/da3e73ed-299d-45f5-a070-b2c4d0a2e05c)

---

### 🔹 관리 시스템 (Manager)

1. **CustomerManager.cs**
   - 활성 손님 리스트 관리
   - Spawn / Despawn 처리
   - 전체 인내심 제거, 튜토리얼 모드 등 유틸 제공

2. **CustomerSpawner.cs**
   - 손님 자동 스폰
   - 확률 기반 희귀도 스폰, 주기/최대 수량 제한 가능

3. **TableManager.cs**
   - 좌석 배치 및 대기열(Queue) 관리
   - 손님 퇴장 시 다음 손님 자동 배치
   - 줄 재정렬 및 강제 퇴장 처리 가능

4. **PoolManager.cs**
   - 오브젝트 풀링 관리
   - 다수의 손님을 효율적으로 생성/회수

---

## 2️⃣ 코드 동작 흐름

1. **손님 생성**
   - `CustomerSpawner` → `CustomerManager.SpawnCustomer()` 호출
   - `PoolManager`에서 손님 객체를 풀링 후 `CustomerController.Setup()` 실행

2. **손님 이동 & 상태 전환**
   - `CustomerController`에서 상태 머신(`CustomerState`) 실행
   - `MovingToEntrance` → 테이블 할당 → `MovingToSeat` → `Ordering` …

3. **손님 행동 이벤트**
   - `Customer`(Model)에서 이벤트 발생
     ```csharp
     OnOrderPlaced?.Invoke(orderData);
     OnPatienceChanged?.Invoke(patienceRatio);
     ```
   - `CustomerController`가 받아서 View/UI 갱신

4. **손님 퇴장**
   - `LeavingState` 진입 시 → `CustomerManager.DespawnCustomer()`
   - `PoolManager`를 통해 객체 반환

---

## 3️⃣ 설계 장점

1. **책임 분리**
   - 데이터 / 시각화 / 로직 명확히 분리
   - 코드 가독성 및 유지보수성 ↑

2. **확장성**
   - 새로운 손님 타입, 행동 상태, 이펙트 쉽게 추가 가능

3. **최적화**
   - 풀링(Object Pooling)으로 런타임 GC 최소화
   - 대규모 시뮬레이션에도 안정적

---

## 4️⃣ 코드 예시

**상태 전환**
```csharp
private void ChangeState(CustomerState newState)
{
    currentState?.Exit(this);
    currentState = newState;
    currentState?.Enter(this);
}
```

**객체 풀링 사용**
```csharp
GameObject obj = PoolManager.Instance.Get(customerPrefab, spawnPos, Quaternion.identity);
// ...
PoolManager.Instance.Release(customerPrefab, obj);
```

**이벤트 기반 UI 갱신**
```csharp
model.OnPatienceChanged += ratio => view.UpdatePatienceUI(ratio);
model.OnOrderPlaced += order => view.ShowOrderBubble(order);
```
