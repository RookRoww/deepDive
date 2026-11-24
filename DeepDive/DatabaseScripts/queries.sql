
SELECT UserId, WindowsUsername, DisplayName, LaunchAtStartup, DefaultWorkspaceId, IsSetupComplete, CreatedAt
FROM Users
ORDER BY UserId ASC;

SELECT UserId, WindowsUsername, DisplayName, LaunchAtStartup, DefaultWorkspaceId, IsSetupComplete, CreatedAt
FROM Users
WHERE UserId = $1;

SELECT UserId, WindowsUsername, DisplayName, LaunchAtStartup, DefaultWorkspaceId, IsSetupComplete, CreatedAt
FROM Users
WHERE WindowsUsername = $1;


INSERT INTO Users (WindowsUsername, DisplayName, LaunchAtStartup, DefaultWorkspaceId, IsSetupComplete)
VALUES ($1, $2, $3, $4, $5)
RETURNING UserId;


UPDATE Users
SET DisplayName = $2,
    LaunchAtStartup = $3,
    DefaultWorkspaceId = $4,
    IsSetupComplete = $5
WHERE UserId = $1;


DELETE FROM Users
WHERE UserId = $1;


SELECT WorkspaceId, UserId, WorkspaceName, ColorHex, IconKey, DisplayOrder, CreatedAt, UpdatedAt
FROM Workspaces
WHERE UserId = $1
ORDER BY DisplayOrder ASC, WorkspaceId ASC;

SELECT WorkspaceId, UserId, WorkspaceName, ColorHex, IconKey, DisplayOrder, CreatedAt, UpdatedAt
FROM Workspaces
WHERE WorkspaceId = $1 AND UserId = $2;

SELECT WorkspaceId
FROM Workspaces
WHERE UserId = $1
  AND LOWER(TRIM(WorkspaceName)) = LOWER(TRIM($2))
  AND WorkspaceId <> $3;


INSERT INTO Workspaces (UserId, WorkspaceName, ColorHex, IconKey, DisplayOrder, CreatedAt, UpdatedAt)
VALUES ($1, $2, $3, $4, $5, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
RETURNING WorkspaceId;


UPDATE Workspaces
SET WorkspaceName = $3,
    ColorHex = $4,
    IconKey = $5,
    UpdatedAt = CURRENT_TIMESTAMP
WHERE WorkspaceId = $1 AND UserId = $2;

UPDATE Workspaces
SET DisplayOrder = $2,
    UpdatedAt = CURRENT_TIMESTAMP
WHERE WorkspaceId = $1;


DELETE FROM Workspaces
WHERE WorkspaceId = $1 AND UserId = $2;




SELECT ActionTypeId, TypeCode, DisplayName, AllowedFormat
FROM ActionTypes
ORDER BY ActionTypeId ASC;

SELECT ActionTypeId, TypeCode, DisplayName, AllowedFormat
FROM ActionTypes
WHERE TypeCode = $1;


INSERT INTO ActionTypes (TypeCode, DisplayName, AllowedFormat)
VALUES ($1, $2, $3)
RETURNING ActionTypeId;


UPDATE ActionTypes
SET TypeCode = $2,
    DisplayName = $3,
    AllowedFormat = $4
WHERE ActionTypeId = $1;


DELETE FROM ActionTypes
WHERE ActionTypeId = $1;



SELECT ActionId, WorkspaceId, ActionTypeId, ActionName, TargetPath, ActionOrder, IsEnabled
FROM WorkspaceActions
WHERE WorkspaceId = $1
ORDER BY ActionOrder ASC, ActionId ASC;

SELECT a.ActionId, a.WorkspaceId, a.ActionTypeId, t.TypeCode, t.DisplayName,
       a.ActionName, a.TargetPath, a.ActionOrder, a.IsEnabled
FROM WorkspaceActions a
JOIN ActionTypes t ON a.ActionTypeId = t.ActionTypeId
WHERE a.WorkspaceId = $1 AND a.IsEnabled = TRUE
ORDER BY a.ActionOrder ASC, a.ActionId ASC;


INSERT INTO WorkspaceActions (WorkspaceId, ActionTypeId, ActionName, TargetPath, ActionOrder, IsEnabled)
VALUES ($1, $2, $3, $4, $5, $6)
RETURNING ActionId;


UPDATE WorkspaceActions
SET ActionTypeId = $3,
    ActionName = $4,
    TargetPath = $5,
    ActionOrder = $6,
    IsEnabled = $7
WHERE ActionId = $1 AND WorkspaceId = $2;

UPDATE WorkspaceActions
SET IsEnabled = $2
WHERE ActionId = $1;


DELETE FROM WorkspaceActions
WHERE ActionId = $1 AND WorkspaceId = $2;

DELETE FROM WorkspaceActions
WHERE WorkspaceId = $1;


SELECT ResultId, WorkspaceId, ActionId, LaunchedAt, StatusCode, StatusMessage, NeedsAttention
FROM LaunchResults
WHERE WorkspaceId = $1
ORDER BY LaunchedAt DESC, ResultId DESC;

SELECT ResultId, WorkspaceId, ActionId, LaunchedAt, StatusCode, StatusMessage, NeedsAttention
FROM LaunchResults
WHERE WorkspaceId = $1 AND NeedsAttention = TRUE
ORDER BY LaunchedAt DESC, ResultId DESC;

INSERT INTO LaunchResults (WorkspaceId, ActionId, LaunchedAt, StatusCode, StatusMessage, NeedsAttention)
VALUES ($1, $2, CURRENT_TIMESTAMP, $3, $4, $5)
RETURNING ResultId;


UPDATE LaunchResults
SET StatusCode = $2,
    StatusMessage = $3,
    NeedsAttention = $4
WHERE ResultId = $1;


DELETE FROM LaunchResults
WHERE ResultId = $1;

DELETE FROM LaunchResults
WHERE LaunchedAt < CURRENT_TIMESTAMP - ($1 || ' days')::INTERVAL;