using Asms.Api.Authorization;
using Asms.Application.Students;
using Asms.Contracts.Auth;
using Asms.Contracts.Students;
using Asms.Domain.Security;
using Microsoft.AspNetCore.Mvc;

namespace Asms.Api.Controllers;

[ApiController]
[Route("api/students")]
public sealed class StudentsController : ControllerBase
{
    private readonly IStudentService _students;

    public StudentsController(IStudentService students)
    {
        _students = students;
    }

    [HttpGet]
    [HasPermission(Permissions.Students.View)]
    [ProducesResponseType(typeof(PagedResult<StudentListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Search(
        [FromQuery] StudentSearchQuery query,
        CancellationToken ct)
    {
        SetNoStore();
        var result = await _students.SearchAsync(query, ct);
        return result.Succeeded
            ? Ok(result.Value)
            : Failure(result.ErrorCode!, result.Duplicates);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Students.View)]
    [ProducesResponseType(typeof(StudentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        SetNoStore();
        var result = await _students.GetAsync(id, ct);
        return result.Succeeded
            ? Ok(result.Value)
            : Failure(result.ErrorCode!, result.Duplicates);
    }

    [HttpPost]
    [HasPermission(Permissions.Students.Create)]
    [ProducesResponseType(typeof(StudentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStudentRequest request,
        CancellationToken ct)
    {
        SetNoStore();
        var result = await _students.CreateAsync(request, ct);
        if (!result.Succeeded)
        {
            return Failure(result.ErrorCode!, result.Duplicates);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Students.Edit)]
    [ProducesResponseType(typeof(StudentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateStudentRequest request,
        CancellationToken ct)
    {
        SetNoStore();
        var result = await _students.UpdateAsync(id, request, ct);
        return result.Succeeded
            ? Ok(result.Value)
            : Failure(result.ErrorCode!, result.Duplicates);
    }

    private IActionResult Failure(
        string code,
        IReadOnlyList<DuplicateMatchDto> duplicates)
    {
        var status = code switch
        {
            "FORBIDDEN" => StatusCodes.Status403Forbidden,
            "STUDENT_NOT_FOUND" => StatusCodes.Status404NotFound,
            "STUDENT_DUPLICATE_WARNING" or
                "STUDENT_VERSION_CONFLICT" or
                "STUDENT_NUMBER_TAKEN" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        if (code == "STUDENT_DUPLICATE_WARNING")
        {
            return StatusCode(status, new { code, duplicates });
        }

        return StatusCode(status, new ErrorResponse(code));
    }

    private void SetNoStore() =>
        Response.Headers.CacheControl = "no-store";
}
