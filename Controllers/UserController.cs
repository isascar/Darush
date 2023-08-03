namespace MyJob.Controllers;


public class UsersController : BaseApiController
{
    public DataContext _context { get; }
    public ITokenService _tokenService { get; }
    private readonly IMapper _mapper;

    Dictionary<string, string> Types = new()
    {
        {"doc","application/msword" },
        {"docx","application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        {"pdf","application/pdf" }
    };

    const int maxSizeInBytes = 100000;
    const int maxCVs = 5;
    string[] SupportedFormats = new string[] { /*"doc",*/ "docx" };

    public UsersController(DataContext context, ITokenService tokenService, IMapper mapper)
    {
        _mapper = mapper;
        _context = context;
        _tokenService = tokenService;
    }

    [HttpGet("Get-all-cvs")]
    public async Task<ActionResult<List<object>>> GetAllCVs()
    {
        // Check user
        var user = await GetUser();
        if (user == null)
            return NotFound();

        return Ok(GetAllActualCv(user).Select(x => new { x.IsDefault, x.Name, x.DateOfAdded }).ToList());
    }

    [HttpGet("Get-cv/{CvId}")]
    public async Task<ActionResult> GetCV(int CvId)
    {
        // Check user   
        var user = await GetUser();
        
        if (user == null)
            return NotFound();

        var cv = GetAllActualCv(user).ElementAtOrDefault(CvId);
        return (cv is null) ?
            BadRequest("CV not exist")
            :
            new FileContentResult(cv.FileContent, Types["docx"])
            // ToDo: Types[user.CVs[CvId].FileContent.FileName.Split(".").Last()])
            {
                FileDownloadName = cv.Name
            };
    }

    [HttpPut("set-cv-as-default/{CvId}")]
    public async Task<ActionResult> SetCVAsDefault(int CvId)
    {
        var user = await GetUser();
        if (user == null)
            return NotFound();

        if (GetAllActualCv(user).ElementAtOrDefault(CvId) is not null)
        {
            GetAllActualCv(user).ForEach(x => x.IsDefault = false);
            GetAllActualCv(user)[CvId].IsDefault = true;
        }
        return (await _context.SaveChangesAsync()) > 0 ? NoContent() : BadRequest("Problem occurred.");
    }
    
    [HttpPut("cv-Change-Name/{CvId}")]
    public async Task<ActionResult> CVChangeName(int CvId, string newName)
    {
        var user = await GetUser();
        if (user == null)
            return NotFound();

        if (GetAllActualCv(user).Count > CvId)
            GetAllActualCv(user)[CvId].Name = newName;
        
        return (await _context.SaveChangesAsync()) > 0 ? NoContent() : BadRequest("Problem occurred.");
    }


    [HttpPost("add-cv")]
    public async Task<ActionResult> AddCV([FromForm] CvDto cv)
    {
        // Check user
        var user = await GetUser();
        if (user == null)
            return NotFound();

        // Check file
        if (cv.File == null || cv.File.Length == 0)
            return BadRequest("No file was uploaded.");
        if (cv.File.Length > maxSizeInBytes)
            return BadRequest("File too large. The file must be up to 100 KB.");
        if (!SupportedFormats.Contains(cv.File.FileName.Split(".").Last()))
            return BadRequest("The system only accepts files in Word format.");

        // Check capacity
        if (user.CVs.Count(x => !x.Deleted) >= maxCVs)
            return BadRequest("It is not possible to add another file to your CV list.");


        using (var stream = new MemoryStream())
        {
            await cv.File.CopyToAsync(stream);
            user.CVs.Add(new CV()
            {
                Name = cv.Name,
                FileContent = stream.ToArray(),
                IsDefault = !user.CVs.Any(x => !x.Deleted)
            });
        }
        return (await _context.SaveChangesAsync()) > 0 ? NoContent() : BadRequest("Problem adding CV.");
    }

    [HttpDelete("delete-cv/{CvId}")]
    public async Task<ActionResult> DeleteCv(int CvId)
    {
        // Check user
        var user = await GetUser();
        if (user == null)
            return NotFound();


        var cv = GetAllActualCv(user).ElementAtOrDefault(CvId);
        if (cv is null)
            return BadRequest("CV not exist");

        // delete
        cv.Deleted = true;
        return (await _context.SaveChangesAsync()) > 0 ? NoContent() : BadRequest("Problem occurred.");
    }

    public async Task<AppUser> GetUser()
    {
        var usName = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return await _context.Users.Include(p => p.CVs).FirstOrDefaultAsync(x => x.UserName == usName);
    }

    public List<CV> GetAllActualCv(AppUser user)
    {
        return  user.CVs.Where(x => !x.Deleted).ToList();
    }
}