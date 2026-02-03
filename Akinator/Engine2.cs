using System;
using System.Collections.Generic;
using System.Linq;

namespace Akinator
{
    public class Engine2
    {
        public static Engine2 Instance { get; private set; }
        public static DBLoader DBLoader { get; private set; }

        public Dictionary<int, int> QSortDict { get; private set; }

        public int DnCounter { get; private set; }
        public int CurrentQuesId { get; private set; }

        const float ConfidenceThreshold = 0.2f;
        const int MaxQuestions = 10;

        public Dictionary<int, float> CharacterConfidence = new Dictionary<int, float>();
        public HashSet<int> AskedQuestions = new HashSet<int>();

        //public Dictionary<string, PlayerAction> PlayerActionDic;
        //public PlayerAction PlayerAction { get; private set; }

        public bool IsRunning { get; private set; }
        public InterfaceType CurrentIntefaceType { get; private set; }


        public void Init()
        {
            if (Instance != null) return;
            Instance = this;
        }

        public void InitProgram()
        {
            DBLoader = new DBLoader();
            DBLoader.Init();
            CurrentIntefaceType = InterfaceType.StartMenu;


            IsRunning = true;
            DnCounter = 0;
            QSortDict = new Dictionary<int, int>();
        }
        public void StartProgram()
        {
            while (IsRunning)
            {
                //CreateAction();
                PrintMenu();
                HandleInput();
            }
        }
        private void PrintMenu()
        {
            switch (CurrentIntefaceType)
            {
                case InterfaceType.StartMenu:
                    Console.WriteLine("\n=== Главное меню ===");
                    Console.WriteLine("1. Начать игру");
                    Console.WriteLine("0. Выход");
                    Console.Write("\nВыберите действие: ");
                    break;
                case InterfaceType.Questioning:
                    Questing();
                    Console.WriteLine("1. Да");
                    Console.WriteLine("2. Нет");
                    Console.WriteLine("3. Не знаю");
                    Console.WriteLine("0. Выход");
                    Console.Write("\nВыберите действие: ");
                    break;
                case InterfaceType.AddCharacter:

                    break;
                default:
                    break;
            }

        }
        private void HandleInput()
        {
            string input = Console.ReadLine();
            Console.Clear();
            switch (input)
            {
                case "1":
                    if (CurrentIntefaceType == InterfaceType.StartMenu) CurrentIntefaceType = InterfaceType.Questioning;
                    PlayerAnswer(1);
                    break;

                case "2":
                    if (CurrentIntefaceType == InterfaceType.Questioning) PlayerAnswer(0);
                    break;

                case "3":
                    if (CurrentIntefaceType == InterfaceType.Questioning) PlayerAnswer(-1);
                    break;

                //case "5":
                //    DeleteAttendanceUI();
                //    break;

                case "0":
                    IsRunning = false;
                    Console.WriteLine("Программа завершена.");
                    break;

                default:
                    Console.WriteLine("Неверный ввод.");
                    break;
            }
        }
        public void Questing()
        {

            // 1) Если CharacterConfidence пуст, инициализируем равными вероятностями
            if (CharacterConfidence.Count == 0)
            {
                foreach (var ch in DBLoader.Characters)
                    CharacterConfidence[ch.Key] = 1f / DBLoader.Characters.Count;
            }

            // 2) Словарь для энтропии каждого вопроса
            Dictionary<int, double> entropyScores = new Dictionary<int, double>();

            // 3) Получаем список кандидатов (те, у кого уверенность > 0)
            var candidates = CharacterConfidence.Where(c => c.Value > 0).Select(c => c.Key).ToList();

            // 4) Если первый вопрос, берем любой, затрагивающий всех
            foreach (var qPair in DBLoader.Questions)
            {
                int qId = qPair.Key;
                if (AskedQuestions.Contains(qId)) continue;

                // Энтропия для вопроса
                Dictionary<int, double> answerSums = new Dictionary<int, double>(); // -1,0,1
                double totalProb = 0;

                foreach (var cId in candidates)
                {
                    var ans = DBLoader.Answers.Values
                        .FirstOrDefault(a => a.CharacterId == cId && a.QuestionId == qId);

                    // Если ответа нет, считаем его равным 0
                    sbyte ansValue = ans?.Value ?? 0;

                    double p = CharacterConfidence[cId];
                    totalProb += p;
                    if (!answerSums.ContainsKey(ansValue)) answerSums[ansValue] = 0;
                    answerSums[ansValue] += p;
                }

                double H = 0;
                foreach (var sum in answerSums.Values)
                {
                    double pv = sum / totalProb;
                    if (pv > 0)
                        H -= pv * Math.Log(pv, 2);
                }

                entropyScores[qId] = H;
            }

            // 5) Если это не первый вопрос — берем вопрос из положительных ответов наиболее вероятного персонажа
            if (CharacterConfidence.Count > 0)
            {
                var topCharId = CharacterConfidence.OrderByDescending(c => c.Value).First().Key;
                var topCharAnswers = DBLoader.Answers.Values
                    .Where(a => a.CharacterId == topCharId && a.Value == 1 && !AskedQuestions.Contains(a.QuestionId))
                    .Select(a => a.QuestionId)
                    .ToList();

                // Берем вопрос с максимальной энтропией среди этих
                if (topCharAnswers.Count > 0)
                {
                    var bestQ = topCharAnswers.OrderByDescending(q => entropyScores.ContainsKey(q) ? entropyScores[q] : 0).First();
                    CurrentQuesId = bestQ;
                    AskedQuestions.Add(bestQ);
                    Console.WriteLine($"Вопрос (фокус на персонаже): {DBLoader.Questions[bestQ].Text}");
                    return;
                }
            }

            // 6) Если топ-персонажа нет или нет вопросов — берем вопрос с максимальной энтропией среди всех
            var questionIdToAsk = entropyScores.OrderByDescending(x => x.Value)
                .Select(x => x.Key)
                .FirstOrDefault(q => !AskedQuestions.Contains(q));

            if (questionIdToAsk != 0)
            {
                CurrentQuesId = questionIdToAsk;
                AskedQuestions.Add(questionIdToAsk);
                Console.WriteLine($"Вопрос: {DBLoader.Questions[questionIdToAsk].Text}");
            }
        }
        public void PlayerAnswer(sbyte playerAnswer)
        {
            Console.Clear();
            if (CharacterConfidence.Count == 0)
            {
                foreach (var ch in DBLoader.Characters)
                    CharacterConfidence[ch.Key] = 1f / DBLoader.Characters.Count;
            }

            // 2) Для каждого персонажа сверяем его ответ
            foreach (var ch in DBLoader.Characters)
            {
                int characterId = ch.Key;

                // Находим ответ персонажа на вопрос
                var realAnswer = DBLoader.Answers.Values
                    .FirstOrDefault(a => a.CharacterId == characterId && a.QuestionId == CurrentQuesId);

                // Если записи нет, считаем что правильный ответ = 0 ("нет")
                sbyte correctAnswer = realAnswer?.Value ?? 0;

                if (playerAnswer == -1) // "Не знаю"
                {
                    CharacterConfidence[characterId] *= 0.9f;
                }
                else if (correctAnswer == playerAnswer) // Ответ совпал
                {
                    CharacterConfidence[characterId] *= 1.2f;
                }
                else // Ответ не совпал
                {
                    CharacterConfidence[characterId] = 0f;
                }
            }

            // 3) Нормируем уверенность, чтобы сумма = 1
            float sum = CharacterConfidence.Values.Sum();
            if (sum > 0)
            {
                foreach (var id in CharacterConfidence.Keys.ToList())
                    CharacterConfidence[id] /= sum;
            }

            // 4) Выводим персонажей по уверенности
            Console.WriteLine("Текущая уверенность в персонажах:");
            bool guessed = false;

            foreach (var pair in CharacterConfidence.OrderByDescending(p => p.Value))
            {
                string name = DBLoader.Characters.ContainsKey(pair.Key) ? DBLoader.Characters[pair.Key].Name : "Unknown";
                Console.WriteLine($"{name}: {pair.Value:P1}");

                // Если уверенность превысила порог И это первый персонаж в списке
                if (!guessed && (pair.Value >= ConfidenceThreshold || MaxQuestions <= AskedQuestions.Count))
                {
                    Console.WriteLine($"\nВы загадали {name}?");
                    Console.WriteLine("1. Да");
                    Console.WriteLine("2. Нет");
                    Console.WriteLine("3. Не знаю");
                    Console.Write("\nВыберите действие: ");

                    // Здесь должна быть логика для обработки ответа на догадку
                    // Например, можно добавить флаг, что сейчас идет догадка
                    guessed = true;
                    break;
                }
            }

            // Если никто не угадан и вопросы не кончились, продолжаем
            if (!guessed && CharacterConfidence.Values.Max() > 0)
            {
                Console.WriteLine("\nСледующий вопрос:");
                Questing();
            }
            else if (CharacterConfidence.Values.Max() == 0)
            {
                Console.WriteLine("\nНе могу угадать! Возможно, такого персонажа нет в базе.");
                // Сброс игры или возврат в меню
                CharacterConfidence.Clear();
                AskedQuestions.Clear();
                CurrentIntefaceType = InterfaceType.StartMenu;
            }
        }
    }
    #region CreateActionMethods
    //public void CreateAction()
    //{

    //    PlayerActionDic.Clear();


    //    switch (CurrentIntefaceType)
    //    {
    //        case InterfaceType.StartMenu: CreateActionForStartMenu(); break;
    //        case InterfaceType.Questioning: CreateActionForInventory(); break;
    //        case InterfaceType.AddCharacter: CreateActionForAttack(); break;

    //    }


    //}

    //public void CreateActionForLocation()
    //{
    //    int InputId = 1;
    //    PlayerAction OpenIntentory = new PlayerAction(InputId, PlayerActionType.);
    //    InputId++;
    //    PlayerActionDic[OpenIntentory.InputId.ToString()] = OpenIntentory;
    //    LocationData loc = Player.CurrentLocation;

    //    PlayerAction OpenMap = new PlayerAction(InputId, PlayerActionType.OpenMap);
    //    InputId++;
    //    PlayerActionDic[OpenMap.InputId.ToString()] = OpenMap;

    //    foreach (KeyValuePair<int, Mob> item in loc.Mobs)
    //    {
    //        PlayerAction attackMob = new PlayerAction(InputId, PlayerActionType.InitAttackMod, item.Value.Id);
    //        InputId++;
    //        PlayerActionDic[attackMob.InputId.ToString()] = attackMob;
    //    }

    //    foreach (KeyValuePair<int, int> item in loc.Items)
    //    {
    //        PlayerAction pickupItem = new PlayerAction(InputId, PlayerActionType.PickupItem, item.Key);
    //        InputId++;
    //        PlayerActionDic[pickupItem.InputId.ToString()] = pickupItem;
    //    }

    //    List<int> gate = loc.LocationDict.Gate;
    //    for (int i = 0; i < gate.Count; i++)
    //    {
    //        PlayerAction moveToLoc = new PlayerAction(InputId, PlayerActionType.MoveToLocation, gate[i]);
    //        InputId++;
    //        PlayerActionDic[moveToLoc.InputId.ToString()] = moveToLoc;
    //    }
    //    PlayerAction exitGame = new PlayerAction(InputId, PlayerActionType.ExitGame);
    //    InputId++;
    //    PlayerActionDic[exitGame.InputId.ToString()] = exitGame;
    //}
    #endregion

}
