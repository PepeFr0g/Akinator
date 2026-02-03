using System;
using System.Collections.Generic;
using Npgsql;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Data;
using System.Threading;

namespace Akinator
{
    public class DBLoader
    {
        public static DBLoader Instance { get; private set; }
        public Dictionary<int, Character> Characters { get; private set; }
        public Dictionary<int, Answer> Answers { get; private set; }
        public Dictionary<int, Question> Questions { get; private set; }

        public string ConnectionString = "Server=localhost;Port=5432;Database=Akinator;User Id = postgres; Password=admin";

        private NpgsqlConnection _connection;
        private NpgsqlCommand _command;


        public void Init()
        {

            if (Instance != null) return;

            Instance = this;

            _connection = new NpgsqlConnection(ConnectionString);
            _connection.Open();


            LoadCharacter();
            LoadAnswer();
            LoadQuestion();


            _connection.Close();

        }
        public void LoadCharacter()
        {
            Characters = new Dictionary<int, Character>();
            _command = new NpgsqlCommand();
            _command.Connection = _connection;
            _command.CommandType = CommandType.Text;
            var query = "SELECT id, name, play_count FROM character";
            _command.CommandText = query;

            using (var reader = _command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var сharacter = new Character(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetInt32(2)

                );

                    Characters[сharacter.Id] = сharacter;

                    Console.WriteLine($"Загружен персонаж {сharacter.Id} {сharacter.Name}");
                }
            }
        }
        public void LoadAnswer()
        {
            Answers = new Dictionary<int, Answer>();
            _command = new NpgsqlCommand();
            _command.Connection = _connection;
            _command.CommandType = CommandType.Text;
            var query = "SELECT character_id, question_id, answer FROM Answer";
            _command.CommandText = query;

            using (var reader = _command.ExecuteReader())
            {
                int counter = 0;
                while (reader.Read())
                {
                    counter++;
                    var answer = new Answer(
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    sbyte.Parse(reader.GetString(2))
                );

                    Answers[counter] = answer;

                    Console.WriteLine($"Загружен ответ {counter} {answer.CharacterId}");
                }
            }
        }
        public void LoadQuestion()
        {
            Questions = new Dictionary<int, Question>();
            _command = new NpgsqlCommand();
            _command.Connection = _connection;
            _command.CommandType = CommandType.Text;
            var query = "SELECT id, qst, answered_count FROM Question";
            _command.CommandText = query;

            using (var reader = _command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var question = new Question(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetInt32(2)

                );

                    Questions[question.Id] = question;

                    Console.WriteLine($"Загружен вопрос {question.Id}: {question.Text}");
                }
            }
        }
        public int InsertNewCharacter(string name, List<int> positiveQuestionIds)
        {
            int newCharId = -1;

            try
            {
                _connection.Open();
                using (var transaction = _connection.BeginTransaction())
                {
                    using (var cmd = new NpgsqlCommand("INSERT INTO character (name, play_count) VALUES (@name, 0) RETURNING id", _connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("name", name);
                        newCharId = (int)cmd.ExecuteScalar();
                    }

                    foreach (int qId in positiveQuestionIds)
                    {
                        InsertNewAnswerInternal(newCharId, qId, 1, _connection, transaction);
                    }

                    transaction.Commit();

                    Characters[newCharId] = new Character(newCharId, name, 0);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при добавлении персонажа в БД: {ex.Message}");
                newCharId = -1;
            }
            finally
            {
                if (_connection.State == ConnectionState.Open)
                {
                    _connection.Close();
                }
            }

            return newCharId;
        }

        private void InsertNewAnswerInternal(int characterId, int questionId, sbyte value, NpgsqlConnection conn, NpgsqlTransaction transaction)
        {
            using (var cmd = new NpgsqlCommand("INSERT INTO Answer (character_id, question_id, answer) VALUES (@char_id, @quest_id, @ans)", conn, transaction))
            {
                cmd.Parameters.AddWithValue("char_id", characterId);
                cmd.Parameters.AddWithValue("quest_id", questionId);
                cmd.Parameters.AddWithValue("ans", value.ToString());
                cmd.ExecuteNonQuery();
            }
        }
        public int InsertNewQuestion(string questionText)
        {
            int newQstId = -1;

            try
            {
                _connection.Open();
                using (var cmd = new NpgsqlCommand("INSERT INTO Question (qst, answered_count) VALUES (@qst, 0) RETURNING id", _connection))
                {
                    cmd.Parameters.AddWithValue("qst", questionText);
                    newQstId = (int)cmd.ExecuteScalar();
                }

                Questions[newQstId] = new Question(newQstId, questionText, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при добавлении вопроса в БД: {ex.Message}");
                newQstId = -1;
            }
            finally
            {
                if (_connection.State == ConnectionState.Open)
                {
                    _connection.Close();
                }
            }

            return newQstId;
        }
    }
}
