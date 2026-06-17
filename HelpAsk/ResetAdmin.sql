-- Сброс пароля администратора на admin123
-- Важно: префикс N обязателен — C# кодирует строку в UTF-16LE (Encoding.Unicode)
UPDATE Users
SET PasswordHash = HASHBYTES('SHA2_512', N'admin123')
WHERE Login = 'admin';
