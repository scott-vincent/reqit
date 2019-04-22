using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using reqit.CmdLine;
using reqit.Models;
using reqit.Services;

namespace reqit.Controllers
{
    [Route("{*any}")]
    [ApiController]
    public class DefaultController : ControllerBase
    {
        private readonly IDefaultService service;

        public DefaultController(ICommand command, IDefaultService service)
        {
            this.service = service;
        }

        [HttpGet]
        public ActionResult<string> Get()
        {
            string response;
            try
            {
                response = this.service.DoCall(Api.Methods.GET, HttpContext.Request.Path,
                        HttpContext.Request.Query, null);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException e)
            {
                return BadRequest(e.Message);
            }

            return Ok(response);
        }

        [HttpPut]
        public ActionResult<string> Put()
        {
            string response;
            try
            {
                response = this.service.DoCall(Api.Methods.PUT, HttpContext.Request.Path,
                        HttpContext.Request.Query, HttpContext.Request.Body);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException e)
            {
                return BadRequest(e.Message);
            }

            return Ok(response);
        }

        [HttpPost]
        public ActionResult<string> Post()
        {
            string response;
            try
            {
                response = this.service.DoCall(Api.Methods.POST, HttpContext.Request.Path,
                        HttpContext.Request.Query, HttpContext.Request.Body);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException e)
            {
                return BadRequest(e.Message);
            }

            return Ok(response);
        }

        [HttpPatch]
        public ActionResult<string> Patch()
        {
            string response;
            try
            {
                response = this.service.DoCall(Api.Methods.PATCH, HttpContext.Request.Path,
                        HttpContext.Request.Query, HttpContext.Request.Body);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException e)
            {
                return BadRequest(e.Message);
            }

            return Ok(response);
        }

        [HttpDelete]
        public ActionResult<string> Delete()
        {
            string response;
            try
            {
                response = this.service.DoCall(Api.Methods.DELETE, HttpContext.Request.Path,
                        HttpContext.Request.Query, null);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException e)
            {
                return BadRequest(e.Message);
            }

            return Ok(response);
        }
    }
}
