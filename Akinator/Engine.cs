using System;
using System.Collections.Generic;
using System.Linq;

namespace Akinator
{
    public class Engine
    {
        public static Engine Instance { get; private set; }
        public static DBLoader DBLoader { get; private set; }

        public int CurrentQuesId { get; private set; }


        const float ConfidenceThreshold = 0.85f;
        const int MaxQuestions = 20;
        private int _dKnowThreshold; 


        public Dictionary<int, float> CharacterConfidence = new Dictionary<int, float>();
        public HashSet<int> AskedQuestions = new HashSet<int>();


        private Dictionary<int, sbyte> _currentAnswerLog = new Dictionary<int, sbyte>();

        private Dictionary<int, HashSet<int>> _characterTraits;


        private string _newCharacterName;
        private List<int> _newCharacterPositiveQuestions;

        public bool IsRunning { get; private set; }
        public InterfaceType CurrentIntefaceType { get; private set; }

        public void Init() { if (Instance == null) Instance = this; }

        public void InitProgram()
        {
            DBLoader = new DBLoader();
            DBLoader.Init();

            CacheTraits();

            CurrentIntefaceType = InterfaceType.StartMenu;
            IsRunning = true;
        }

        private void CacheTraits()
        {
            _characterTraits = new Dictionary<int, HashSet<int>>();
            foreach (int charId in DBLoader.Characters.Keys)
            {
                _characterTraits[charId] = new HashSet<int>();
            }

            foreach (var ans in DBLoader.Answers.Values)
            {
                if (ans.Value == 1 && _characterTraits.ContainsKey(ans.CharacterId))
                {
                    _characterTraits[ans.CharacterId].Add(ans.QuestionId);
                }
            }
        }

        public void StartProgram()
        {
            while (IsRunning)
            {
                PrintMenu();
                HandleInput();
            }
        }

        private void PrintMenu()
        {
            Console.WriteLine();
            switch (CurrentIntefaceType)
            {
                case InterfaceType.StartMenu:
                    Console.WriteLine("=== Главное меню ===");
                    Console.WriteLine("1. Начать игру");
                    Console.WriteLine("0. Выход");
                    break;
                case InterfaceType.Questioning:
                    if (CurrentQuesId == 0) Questing();
                    Console.WriteLine("1. Да");
                    Console.WriteLine("2. Нет");
                    if (_dKnowThreshold <= 2) Console.WriteLine("3. Не знаю");
                    Console.WriteLine("0. Меню");
                    break;
                case InterfaceType.AkinatorGuessed:
                    Console.WriteLine("1. Да, это он!");
                    Console.WriteLine("2. Нет, ты не угадал.");
                    Console.WriteLine("0. Меню");
                    break;
                case InterfaceType.AddCharacter:
                    break;
            }
            Console.Write("Ввод: ");
        }

        private void HandleInput()
        {
            string input = Console.ReadLine();
            Console.Clear();

            if (CurrentIntefaceType == InterfaceType.AddCharacter)
            {
                AddCharacterStep(input);
                return;
            }

            switch (input)
            {
                case "1":
                    if (CurrentIntefaceType == InterfaceType.StartMenu) StartNewGame();
                    else if (CurrentIntefaceType == InterfaceType.Questioning) PlayerAnswer(1);
                    else if (CurrentIntefaceType == InterfaceType.AkinatorGuessed) AkinatorAnswered(true);
                    break;
                case "2":
                    if (CurrentIntefaceType == InterfaceType.Questioning) PlayerAnswer(-1);
                    else if (CurrentIntefaceType == InterfaceType.AkinatorGuessed)
                    {
                        if (AskedQuestions.Count >= MaxQuestions)
                        {
                            AkinatorAnswered(false);
                        }
                        else
                        {
                            PlayerAnswer(0);
                            CurrentIntefaceType = InterfaceType.Questioning;
                        }
                    }
                    break;
                case "3":
                    if (CurrentIntefaceType == InterfaceType.Questioning && _dKnowThreshold < 2)
                    {
                        PlayerAnswer(0);
                        _dKnowThreshold++;
                    }
                    break;
                case "0":
                    if (CurrentIntefaceType == InterfaceType.Questioning || CurrentIntefaceType == InterfaceType.AkinatorGuessed) CurrentIntefaceType = InterfaceType.StartMenu;
                    else IsRunning = false;
                    break;
            }
        }

        private void StartNewGame()
        {
            CurrentIntefaceType = InterfaceType.Questioning;
            CharacterConfidence.Clear();
            AskedQuestions.Clear();
            _currentAnswerLog.Clear();
            CurrentQuesId = 0;
            _dKnowThreshold = 0;

            foreach (var key in DBLoader.Characters.Keys)
            {
                CharacterConfidence[key] = 1.0f / DBLoader.Characters.Count;
            }

            Questing();
        }


        public void Questing()
        {
            var activeCandidates = CharacterConfidence.Where(c => c.Value > 0.001f).ToList();

            if (activeCandidates.Count <= 1 || AskedQuestions.Count >= MaxQuestions)
            {
                FinishRound();
                return;
            }

            int bestQuestionId = -1;
            double bestDiff = double.MaxValue; 

            foreach (var qId in DBLoader.Questions.Keys)
            {
                if (AskedQuestions.Contains(qId)) continue;

                double probYes = 0.0;
                double totalProb = activeCandidates.Sum(c => c.Value);

                foreach (var candidate in activeCandidates)
                {
                    if (_characterTraits[candidate.Key].Contains(qId))
                    {
                        probYes += candidate.Value;
                    }
                }

                double pYes = totalProb > 0 ? probYes / totalProb : 0;

                double diff = Math.Abs(pYes - 0.5);

                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestQuestionId = qId;
                }
            }

            if (bestQuestionId != -1)
            {
                CurrentQuesId = bestQuestionId;
                AskedQuestions.Add(CurrentQuesId);
                Console.WriteLine($"\nВопрос #{AskedQuestions.Count}: {DBLoader.Questions[CurrentQuesId].Text}");
            }
            else
            {
                FinishRound();
            }
        }

        public void PlayerAnswer(sbyte playerAnswer)
        {

            if (playerAnswer == 0)
            {
                Questing();
                return;
            }

            if (CurrentQuesId != 0 && !_currentAnswerLog.ContainsKey(CurrentQuesId))
            {
                _currentAnswerLog.Add(CurrentQuesId, playerAnswer);
            }

            float pMatch = (playerAnswer == 1) ? 2.0f : 1.5f;
            float pMismatch = 0.05f;

            foreach (var key in CharacterConfidence.Keys.ToList())
            {
                bool hasTrait = _characterTraits[key].Contains(CurrentQuesId);

                sbyte expected = hasTrait ? (sbyte)1 : (sbyte)-1;

                if (expected == playerAnswer)
                {
                    CharacterConfidence[key] *= pMatch;
                }
                else
                {
                    CharacterConfidence[key] *= pMismatch;
                }
            }

            NormalizeConfidence();

            var top = CharacterConfidence.OrderByDescending(x => x.Value).First();

            Console.WriteLine($"Топ: {DBLoader.Characters[top.Key].Name} ({top.Value:P2})");

            if (top.Value >= ConfidenceThreshold || AskedQuestions.Count >= MaxQuestions)
            {
                Console.WriteLine($"\nЯ думаю это... {DBLoader.Characters[top.Key].Name}!");
                CurrentIntefaceType = InterfaceType.AkinatorGuessed;
            }
            else
            {
                Questing();
            }
        }


        private void NormalizeConfidence()
        {
            float sum = CharacterConfidence.Values.Sum();
            if (sum > 0)
            {
                foreach (var k in CharacterConfidence.Keys.ToList())
                    CharacterConfidence[k] /= sum;
            }
        }

        private void FinishRound()
        {
            var top = CharacterConfidence.OrderByDescending(x => x.Value).FirstOrDefault();
            string guessName = (top.Key != 0 && DBLoader.Characters.ContainsKey(top.Key)) ? DBLoader.Characters[top.Key].Name : "неизвестный персонаж";

            Console.WriteLine($"\nВопросы кончились. Я думаю, что это {guessName}!");
            CurrentIntefaceType = InterfaceType.AkinatorGuessed;
        }

        private void AkinatorAnswered(bool guessedCorrectly)
        {
            if (guessedCorrectly)
            {
                Console.WriteLine("\nУра! Я угадал!");
                FinishGame();
            }
            else
            {
                Console.WriteLine("\nЭээх, не угадал! Помоги научиться, добавив персонажа.");
                CurrentIntefaceType = InterfaceType.AddCharacter;
                _addCharacterSubStep = 0;
                AddCharacterStep(null);
            }
        }


        private int _addCharacterSubStep = 0;

        private void AddCharacterStep(string input)
        {
            switch (_addCharacterSubStep)
            {
                case 0:
                    Console.WriteLine("\n=== Добавление нового персонажа ===");
                    Console.WriteLine("Кого ты загадал? Введи имя персонажа:");
                    _addCharacterSubStep = 1;
                    break;

                case 1:
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        Console.WriteLine("Имя не может быть пустым. Введи имя персонажа:");
                        return;
                    }
                    _newCharacterName = input.Trim();
                    _newCharacterPositiveQuestions = new List<int>();

                    Console.WriteLine($"\nСпасибо! Теперь ты можешь добавить новый вопрос, который описывает твоего персонажа.");

                    _addCharacterSubStep = 2;
                    goto case 2; 

                case 2:
                    Console.WriteLine($"\nВведи текст нового вопроса (например, 'Бренд из германии?').");
                    Console.WriteLine("Или введи 0, если хочешь завершить добавление.");
                    _addCharacterSubStep = 3;
                    break;

                case 3:
                    if (input == "0" || string.IsNullOrWhiteSpace(input))
                    {
                        FinalizeAddCharacter();
                        break;
                    }

                    Console.WriteLine($"\nДобавление нового вопроса: '{input.Trim()}'");
                    DBLoader.InsertNewQuestion(input.Trim());

                    _newCharacterPositiveQuestions.Add(DBLoader.Questions.Last().Key);

                    _addCharacterSubStep = 2;
                    goto case 2;
            }
        }

        private void FinalizeAddCharacter()
        {

            var finalPositiveAnswers = _currentAnswerLog
                                        .Where(pair => pair.Value == 1)
                                        .Select(pair => pair.Key)
                                        .ToList();

            finalPositiveAnswers.AddRange(_newCharacterPositiveQuestions);

            int newCharId = DBLoader.InsertNewCharacter(_newCharacterName, finalPositiveAnswers);

            Console.WriteLine($"\n--- Сохранение ---");
            Console.WriteLine($"Персонаж '{_newCharacterName}' (ID: {newCharId}) добавлен в базу.");
            Console.WriteLine($"Количество положительных ответов: {finalPositiveAnswers.Count}.");

            FinishGame();
        }

        private void FinishGame()
        {
            Console.WriteLine("Нажми Enter для выхода в меню.");
            Console.ReadLine();
            CurrentIntefaceType = InterfaceType.StartMenu;
            _addCharacterSubStep = 0;
        }
    }
}