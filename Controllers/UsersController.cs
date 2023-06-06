using Newtonsoft.Json;
using API_Implementation.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace API_Implementation.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public UsersController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("UsersCon");
        }

        public delegate Task<IActionResult> RequestDelegate();

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                string query = @"SELECT * FROM users ORDER BY id ASC";
                List<UsersModel> users = new List<UsersModel>();
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (NpgsqlCommand command = new NpgsqlCommand(query, conn))
                    {
                        NpgsqlDataReader myReader = await command.ExecuteReaderAsync();
                        while (myReader.Read())
                        {
                            UsersModel user = new UsersModel
                            {
                                Id = Convert.ToInt32(myReader["id"]),
                                Age = Convert.ToInt32(myReader["age"]),
                                Name = myReader["name"].ToString(),
                                Email = myReader["email"].ToString()
                            };
                            users.Add(user);
                        }
                    }
                }
                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }


        [HttpPost]
        public async Task<IActionResult> AddUsers(UsersModel user)
        {
            try
            {
                string query = @"INSERT INTO users (name, email, age) VALUES (@Name, @Email, @Age);";
                using (NpgsqlConnection myCon = new NpgsqlConnection(_connectionString))
                {
                    await myCon.OpenAsync();
                    using (NpgsqlCommand myCommand = new NpgsqlCommand(query, myCon))
                    {
                        myCommand.Parameters.AddWithValue("@Name", user.Name ?? (object)DBNull.Value);
                        myCommand.Parameters.AddWithValue("@Email", user.Email ?? (object)DBNull.Value);
                        myCommand.Parameters.AddWithValue("@Age", user.Age);
                        await myCommand.ExecuteNonQueryAsync();
                    }
                }
                return Ok("Added Successfully");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] object updatesObject)
        {
            try
            {
                if (updatesObject == null)
                {
                    return BadRequest("No properties provided for update.");
                }
                var updates = JsonConvert.DeserializeObject<Dictionary<string, object>>(updatesObject.ToString());
                if (updates == null || updates.Count == 0)
                {
                    return BadRequest("No properties provided for update.");
                }
                string query = "UPDATE users SET";
                List<NpgsqlParameter> parameters = new List<NpgsqlParameter>();
                foreach (var kvp in updates)
                {
                    string field = kvp.Key.ToLower(); // Assuming the request body keys match the database field names
                    object value = kvp.Value;
                    query += $" {field} = @{field},";
                    parameters.Add(new NpgsqlParameter($"@{field}", value));
                }
                query = query.TrimEnd(',') + " WHERE id = @Id;";
                parameters.Add(new NpgsqlParameter("@Id", id));
                using (NpgsqlConnection myCon = new NpgsqlConnection(_connectionString))
                {
                    await myCon.OpenAsync();
                    using (NpgsqlCommand myCommand = new NpgsqlCommand(query, myCon))
                    {
                        myCommand.Parameters.AddRange(parameters.ToArray());
                        await myCommand.ExecuteNonQueryAsync();
                    }
                }
                return Ok("Updated Successfully");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUsers(int id)
        {
            try
            {
                string query = @"DELETE FROM users WHERE id = @Id;";
                using (NpgsqlConnection myCon = new NpgsqlConnection(_connectionString))
                {
                    await myCon.OpenAsync();
                    using (NpgsqlCommand myCommand = new NpgsqlCommand(query, myCon))
                    {
                        myCommand.Parameters.AddWithValue("@Id", id);
                        await myCommand.ExecuteNonQueryAsync();
                    }
                }
                return Ok("Deleted Successfully");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        public async Task<IActionResult> RequestHandler(string action, UsersModel user)
        {
            switch (action)
            {
                case "get":
                    return await GetUsers();
                case "post":
                    return await AddUsers(user);
                case "put":
                    var updates = new Dictionary<string, object>();
                    if (user.Name != null)
                        updates.Add("name", user.Name);
                    if (user.Email != null)
                        updates.Add("email", user.Email);
                    if (user.Age != null)
                        updates.Add("age", user.Age);
                    return await UpdateUser(user.Id, updates);
                case "delete":
                    return await DeleteUsers(user.Id);
                default:
                    return BadRequest("Invalid action");
            }
        }
    }
}
