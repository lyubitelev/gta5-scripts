# План: отмена запроса по Z + бесшовное переобращение к педу

Цель — убрать «залипание» во время обработки. Сейчас, пока идёт цепочка STT→LLM→TTS (`_isProcessing == true`), клавиша `Z` полностью игнорируется ([HandleKeyDown](../Ai/AiController.cs)), и игрок заблокирован до конца запроса. В худшем случае (зависший этап до таймаута) ожидание — до **30+20+45 = 95 с**.

Принцип: **`Z` всегда означает «хочу говорить сейчас»** — прерывает всё висящее и тут же начинает новую запись.

---

## Часть 1. Один CancellationToken на взаимодействие

* В `AiController` завести поле `CancellationTokenSource _currentCts`.
* На `KeyUp` (старт обработки) создавать новый `_currentCts`, его токен прокидывать в `ProcessInteractionAsync`.
* `ProcessInteractionAsync(..., CancellationToken token)` передаёт `token` в STT/LLM/TTS.
* В `AiApiService` все сетевые методы получают параметр `CancellationToken`:
  * внутри связать с пер-этапным таймаутом:
    `using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token)) { linked.CancelAfter(StageTimeout); await client.PostAsync(url, content, linked.Token); }`
  * так сохраняются индивидуальные таймауты (STT 30 / LLM 20 / TTS 45) **и** работает внешняя отмена.

## Часть 2. Z во время обработки = отмена + новая запись (бесшовно)

* В `HandleKeyDown`, если идёт обработка/запись:
  1. `_currentCts?.Cancel()` — HTTP-вызов прерывается;
  2. сброс `_isProcessing = false`, остановка текущей записи если есть;
  3. `Notifier.Show("Отменено")`;
  4. **сразу** перейти к старту новой записи (тем же нажатием) — не требуется жать дважды.
* То есть убрать жёсткий ранний выход `if (!_isProcessing && !_recordingService.IsRecording)` — заменить на «если занято → отменить, затем начать заново».

## Часть 3. Корректная обработка отмены

* `OperationCanceledException` / `TaskCanceledException` от **пользовательской** отмены не логировать как `ERROR` и не показывать «AI Error» — это штатное «Отменено». Отличать таймаут от ручной отмены по тому, сработал ли внешний токен.
* **Защита от устаревших ответов:** привязать каждый запрос к своему токену/идентификатору. Если в `_actionQueue` приходит результат от уже отменённого взаимодействия (игрок начал новое) — игнорировать, не применять action и не трогать `_isProcessing` нового запроса.
* Аккуратно `Dispose` старого `_currentCts` при создании нового.

---

## Часть 4 (связанное). Не сбивать `StandStill`-ом педа в кооперативной задаче

Симптом «садится в машину только со второго раза»: первая FOLLOW-команда корректна, но при повторном `Z` в [HandleKeyDown](../Ai/AiController.cs) педу выдаётся `Task.StandStill(20000)`, что **прерывает** идущий `EnterVehicle`. Затем вторая FOLLOW пере-выдаёт задачу — и пед садится.

**Фикс:** перед выдачей `StandStill` пропускать педа, который сейчас выполняет кооперативную задачу:
* проверка `ped.IsEnteringVehicle` (идёт к машине), либо
* флаг «пед в режиме FOLLOW» (выставлять при FOLLOW, снимать при смене действия).

Тогда обращение к такому педу не будет ломать посадку/следование.

---

## Порядок реализации
1. Прокинуть `CancellationToken` в `AiApiService` (STT/LLM/TTS) + linked-токен с таймаутом.
2. `_currentCts` в `AiController`, отмена + бесшовный рестарт записи по `Z`.
3. Глушение «AI Error» при ручной отмене + защита от устаревших ответов.
4. Не сбивать `StandStill`-ом педа в FOLLOW/посадке.

## Затрагиваемые файлы
* [Ai/AiController.cs](../Ai/AiController.cs) — `_currentCts`, логика `HandleKeyDown`/`HandleKeyUp`, `ProcessInteractionAsync`, гейт `StandStill`.
* [Ai/AiApiService.cs](../Ai/AiApiService.cs) — параметр `CancellationToken` во всех сетевых методах, linked-токены.
