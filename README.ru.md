<p align="center"><img src="docs/icon.png" width="96" alt=""></p>

<h1 align="center">SuperDictate</h1>

<p align="center">
Приватная диктовка для Windows и Mac. Нажмите клавишу, говорите — и слова
появятся там, где стоит курсор, в любом приложении. Речь распознаётся на вашем
компьютере: голос никуда не отправляется.
</p>

<p align="center">
  <a href="https://github.com/ZazaKin/SuperDictate-windows/releases/latest"><img alt="Последний выпуск" src="https://img.shields.io/github/v/release/ZazaKin/SuperDictate-windows?label=release&color=3272AA"></a>
  <img alt="Windows 10 и 11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4">
  <img alt="macOS 14 и новее на Apple Silicon" src="https://img.shields.io/badge/macOS-14%2B%20%C2%B7%20Apple%20Silicon-111111">
  <a href="LICENSE"><img alt="Лицензия: MIT с Commons Clause" src="https://img.shields.io/badge/license-MIT%20%2B%20Commons%20Clause-5B8DEF"></a>
</p>

<p align="center"><a href="README.md">English</a> · <b>Русский</b></p>

<p align="center">
  <img src="docs/images/capsule.gif" width="720" alt="Капсула пишет фразу слово за словом, пока её произносят">
</p>

## Скачать

Обе версии — на странице [последнего выпуска](https://github.com/ZazaKin/SuperDictate-windows/releases/latest).

| | Windows | Mac |
|---|---|---|
| **Файл** | `SuperDictate-Setup-<версия>.exe` | `SuperDictate-macOS-<версия>.zip` |
| **Нужно** | 64-разрядная Windows 10 (2004 или новее) или 11 | Mac на Apple Silicon (M1 или новее), macOS 14 или новее |
| **Распознавание** | Whisper, на процессоре или видеокарте NVIDIA | NVIDIA Parakeet v3, на нейросетевом ядре Apple |
| **Языки** | 20, от английского и испанского до китайского и арабского | 25 европейских |
| **Инструкция** | [Для Windows](docs/user-guide.ru.md) | [Для Mac](docs/mac-guide.ru.md) |

Обе бесплатны. Интернет нужен только один раз — скачать модель распознавания.

## Что умеет

- **Диктовка в любом приложении.** Горячая клавиша (**правый Alt** в Windows,
  **правый ⌘** на Mac) начинает и заканчивает; текст печатается там, где стоит
  курсор. Можно и удерживать клавишу, пока говорите, а в Windows есть плавающая
  кнопка микрофона.
- **Слова видны, пока вы говорите.** Над экраном плавает капсула и пишет
  черновик. Когда вы закончите, запись распознаётся целиком ещё раз, поэтому
  напечатанный текст немного точнее черновика.
- **Капсула на ваш вкус.** Тринадцать обложек, в том числе Liquid Glass. Цвет,
  размер, прозрачность и вид индикатора голоса; любой угол экрана или любое
  место, куда вы её перетащите. У левого или правого края она растёт в сторону
  от края.
- **Ваши языки.** Отметьте языки, на которых говорите: распознавание выбирает
  только среди них. Переключение — из трея (Windows) или строки меню (Mac).
- **Останавливается сама.** Минута тишины завершает диктовку и печатает
  сказанное.
- **Приватно.** Ни аккаунта, ни рекламы, ни аналитики. Звук и текст остаются на
  вашем компьютере.
- **ИИ-правка** (Windows, по желанию). Пунктуация по встроенным правилам,
  локальной моделью через Ollama или облачным сервисом на ваш выбор.

### Капсула

<p align="center">
  <img src="docs/images/capsule-skins.png" width="820" alt="Все тринадцать обложек капсулы с фразой">
</p>

На Mac с macOS 26 обложка Liquid Glass — настоящее стекло Apple; на более
старых Mac и в Windows — близкое воссоздание.

### Windows

<table>
  <tr>
    <td><img src="docs/images/windows/capsule.png" alt="Настройки Windows: страница Capsule с обложками"></td>
    <td><img src="docs/images/windows/languages.png" alt="Настройки Windows: страница Languages с флагами"></td>
  </tr>
  <tr>
    <td><img src="docs/images/windows/dictation.png" alt="Настройки Windows: страница Dictation"></td>
    <td><img src="docs/images/windows/models.png" alt="Настройки Windows: страница Speech model"></td>
  </tr>
</table>

### Mac

<table>
  <tr>
    <td><img src="docs/images/mac/capsule.png" alt="Настройки Mac: вкладка Capsule"></td>
    <td><img src="docs/images/mac/languages.png" alt="Настройки Mac: вкладка Languages"></td>
  </tr>
  <tr>
    <td><img src="docs/images/mac/editor.png" alt="Перемещение капсулы: прилипает к краям и показывает, куда растёт"></td>
    <td><img src="docs/images/mac/menu-bar.png" alt="Панель в строке меню с выбором языка"></td>
  </tr>
</table>

## Установка

### Windows

1. Скачайте `SuperDictate-Setup-<версия>.exe` со страницы
   [последнего выпуска](https://github.com/ZazaKin/SuperDictate-windows/releases/latest)
   и запустите. Если Windows покажет «Система Windows защитила ваш компьютер»,
   нажмите «Подробнее» › «Выполнить в любом случае».
2. Выберите папку и нажмите **Install**. Права администратора не нужны.
3. В открывшихся настройках нажмите **Install** в разделе **Speech runtime**,
   затем **Download** рядом с моделью (рекомендуем Large v3 Turbo).
4. Когда статус станет **Ready**, нажмите **правый Alt**, говорите и нажмите
   его ещё раз.

Подробно, со всеми настройками: [инструкция для Windows](docs/user-guide.ru.md).

### Mac

1. Скачайте `SuperDictate-macOS-<версия>.zip` со страницы
   [последнего выпуска](https://github.com/ZazaKin/SuperDictate-windows/releases/latest),
   откройте его и перетащите **SuperDictate** в **Программы** (Applications).
2. Откройте приложение. Apple его пока не заверила, поэтому в первый раз macOS
   его остановит: откройте **Системные настройки › Конфиденциальность и
   безопасность** и нажмите **Всё равно открыть**.
3. Окно настройки попросит три разрешения (микрофон, универсальный доступ,
   мониторинг ввода) и скачает модель распознавания (около 460 МБ).
4. Нажмите **правый ⌘**, говорите и нажмите его ещё раз.

Подробно, шаг за шагом: [инструкция для Mac](docs/mac-guide.ru.md).

## Обновления

- **Windows:** **Settings › Support › Check for updates**. Обновление
  скачивается, сверяется по SHA-256 и устанавливается по щелчку.
- **Mac:** скачайте новый zip со страницы выпусков и замените приложение в
  папке «Программы».

Сами по себе приложения в сеть не выходят. Что нового в каждой версии — в
[заметках к выпускам](docs/releases/) (на английском).

## Приватность

Звук и текст остаются на вашем компьютере; в журналах нет того, что вы
сказали. Интернет используется только по вашему щелчку: загрузка движка и
моделей, проверка обновлений и, если включить, ИИ-правка через облачный сервис.
Подробности по обоим приложениям — в [PRIVACY.md](PRIVACY.md) (на английском) и
в инструкциях.

## Помощь

- Как что делать и что делать, если не работает: инструкции
  [для Windows](docs/user-guide.ru.md) и [для Mac](docs/mac-guide.ru.md).
- Ошибка или идея: [GitHub Issues](https://github.com/ZazaKin/SuperDictate-windows/issues/new/choose)
  (можно по-русски).
- Уязвимости: см. [SECURITY.md](SECURITY.md).

## Разработчикам

Оба приложения — в этом репозитории:

| Часть | Где |
|---|---|
| Приложение для Windows (.NET 8, WPF) | [`src/SuperDictate`](src/SuperDictate) |
| Приложение для Mac (SwiftUI, пакет Swift) | [`macos/`](macos/README.md) |
| Скрипты сборки, установки и выпуска (Windows) | [`scripts/`](scripts) |
| Документация, картинки, заметки к выпускам | [`docs/`](docs) |
| Сайт | [`website/`](website/README.md) |

Как собрать, проверить, установить версию для разработки и выпустить релиз —
в [docs/development.ru.md](docs/development.ru.md). Сначала прочитайте
[AGENTS.md](AGENTS.md): там правила, которые соблюдает каждое изменение. Как
предложить изменение — в [CONTRIBUTING.md](CONTRIBUTING.md).

## Лицензия

SuperDictate распространяется по лицензии MIT с условием
[Commons Clause](https://commonsclause.com): его можно бесплатно использовать
(в том числе в работе), изменять и распространять, но нельзя продавать — ни
саму программу, ни платные услуги, ценность которых целиком или в основном
строится на ней. Полный текст — в [LICENSE](LICENSE).

Проект основан на [Parakey](https://github.com/rcourtman/parakey) Ричарда
Кортмана; эти части остаются под исходной лицензией MIT, её текст сохранён в
`LICENSE`. Сторонние компоненты и модели — в [NOTICE.md](NOTICE.md).

<a id="donate"></a>

## Поддержать проект

SuperDictate бесплатен. Если он экономит вам время, поддержите разработку:

<!-- donations: build-release.cmd fills this list from release-settings.ini -->
- **Ko-fi**: [ko-fi.com/zazakin](https://ko-fi.com/zazakin) - Card or PayPal
- **Buy Me a Coffee**: [buymeacoffee.com/zazakin](https://buymeacoffee.com/zazakin) - Card
- **USDT / USDC (EVM)**: `0xEB571a20373a439f9c97EAA203b6c86B927B3482` - Same address on Ethereum, BNB Chain, Polygon, Arbitrum, Base
- **USDT (TRC20)**: `TAUv9GctsU5hCbd5K48otaZBt6H5fLULeb` - TRON network only
- **Solana**: `22qrMU3Jk5rrace5sWS88pqo1AoTftmfHNe4QaNs8nTQ` - USDC or SOL on Solana only
- **Bitcoin**: `bc1q28r8zrnug2jhkn4p33836flhkp78dz6neghhe4` - Bitcoin network only
<!-- /donations -->

Те же способы есть в приложении для Windows: **Settings › Support**.
Криптовалюту отправляйте только в сети, указанной рядом с адресом. Спасибо!
